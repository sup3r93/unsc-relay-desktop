using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UnscRelay.Core;

public sealed record SyncEnvelope(int Version, string Owner, string Kdf, int Iterations, string Salt,
    string WrapIv, string WrappedKey, string Iv, string Ciphertext);
public sealed record PortableSnapshot(int Version, JsonElement Records, AppData Data, List<HostKey> HostKeys);
public enum SyncDecision { Unchanged, Push, Pull, Conflict }
public static class VaultSyncPolicy
{
    public static SyncDecision Decide(string baseline, string local, string remote) =>
        local == remote ? SyncDecision.Unchanged : local == baseline ? SyncDecision.Pull :
        remote == baseline ? SyncDecision.Push : SyncDecision.Conflict;
}

public static class VaultSyncCrypto
{
    public const int MaxPlaintext = 32 * 1024 * 1024;
    public static JsonSerializerOptions Json { get; } = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
    internal static JsonSerializerOptions Schema2Json {get;}=new(Json){UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow};
    public static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static byte[] Aad(string owner, string kind) => Encoding.UTF8.GetBytes("UNSC-RELAY-SYNC:2:" + owner + ":" + kind);
    public static void Validate(SyncEnvelope e, string owner)
    {
        if (e is null || owner is null || e.Owner is null || e.Salt is null || e.WrapIv is null || e.WrappedKey is null || e.Iv is null || e.Ciphertext is null || owner.Length != 64 || owner.Any(c => !char.IsAsciiHexDigit(c)) || e.Owner != owner || e.Version != 2 ||
            e.Kdf != "PBKDF2-SHA256" || e.Iterations != 600000 || e.Ciphertext.Length > 48 * 1024 * 1024 ||
            Convert.FromBase64String(e.Salt).Length != 16 || Convert.FromBase64String(e.WrapIv).Length != 12 ||
            Convert.FromBase64String(e.WrappedKey).Length != 48 || Convert.FromBase64String(e.Iv).Length != 12 ||
            Convert.FromBase64String(e.Ciphertext).Length is < 16 or > MaxPlaintext + 16)
            throw new CryptographicException("Archivio online non valido o appartenente a un altro account.");
    }
    private static byte[] Encrypt(byte[] key, byte[] iv, ReadOnlySpan<byte> plaintext, byte[] aad)
    {
        var result = new byte[plaintext.Length + 16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(iv, plaintext, result.AsSpan(0, plaintext.Length), result.AsSpan(plaintext.Length), aad);
        return result;
    }
    private static byte[] Decrypt(byte[] key, byte[] iv, byte[] cipher, byte[] aad)
    {
        var result = new byte[cipher.Length - 16];
        try { using var aes = new AesGcm(key, 16); aes.Decrypt(iv, cipher.AsSpan(0, result.Length), cipher.AsSpan(result.Length), result, aad); return result; }
        catch { CryptographicOperations.ZeroMemory(result); throw; }
    }
    public static SyncEnvelope Create(string owner, string password, byte[] key, byte[] plaintext)
    {
        if (password.Length < 12 || key.Length != 32 || plaintext.Length > MaxPlaintext) throw new ArgumentException("Password o dimensioni dell’archivio non valide.");
        var salt = RandomNumberGenerator.GetBytes(16); var iv = RandomNumberGenerator.GetBytes(12);
        var kek = Rfc2898DeriveBytes.Pbkdf2(password, salt, 600000, HashAlgorithmName.SHA256, 32);
        try {
            var wrapped = Encrypt(kek, iv, key, Aad(owner, "key"));
            return Seal(new(2, owner, "PBKDF2-SHA256", 600000, Convert.ToBase64String(salt), Convert.ToBase64String(iv), Convert.ToBase64String(wrapped), Convert.ToBase64String(new byte[12]), Convert.ToBase64String(new byte[16])), key, plaintext);
        } finally { CryptographicOperations.ZeroMemory(kek); }
    }
    public static SyncEnvelope Seal(SyncEnvelope header, byte[] key, byte[] plaintext)
    {
        if (key.Length != 32 || plaintext.Length > MaxPlaintext) throw new ArgumentException("Dimensione dell’archivio non supportata.");
        Validate(header, header.Owner);
        var iv = RandomNumberGenerator.GetBytes(12);
        return header with { Iv = Convert.ToBase64String(iv), Ciphertext = Convert.ToBase64String(Encrypt(key, iv, plaintext, Aad(header.Owner, "payload"))) };
    }
    public static byte[] UnlockKey(SyncEnvelope envelope, string owner, string password)
    {
        Validate(envelope, owner);
        var kek = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(envelope.Salt), 600000, HashAlgorithmName.SHA256, 32);
        try { return Decrypt(kek, Convert.FromBase64String(envelope.WrapIv), Convert.FromBase64String(envelope.WrappedKey), Aad(owner, "key")); }
        finally { CryptographicOperations.ZeroMemory(kek); }
    }
    public static byte[] Open(SyncEnvelope envelope, string owner, byte[] key)
    {
        Validate(envelope, owner);
        return Decrypt(key, Convert.FromBase64String(envelope.Iv), Convert.FromBase64String(envelope.Ciphertext), Aad(owner, "payload"));
    }
    public static byte[] Capture(KeyVault vault, AppData data, List<HostKey> hosts)
    {
        var records = vault.ExportSyncRecords();
        try {
            using var document = JsonDocument.Parse(records);
            // Device paths, security preferences, Hello enrollment and update tokens remain local.
            var portable = new AppData { PortablePreferences=PortablePreferences.Capture(data.Settings,data.PortablePreferences), ConnectionViews=data.ConnectionViews.OrderBy(view=>view.Id).ToList(), ConnectionTemplates=data.ConnectionTemplates.OrderBy(template=>template.Id,StringComparer.Ordinal).ToList(), Profiles = data.Profiles.OrderBy(p=>p.Id,StringComparer.Ordinal).ToList(),
                Folders = data.Folders.Order(StringComparer.Ordinal).ToList(), Snippets = data.Snippets.OrderBy(s=>s.Id,StringComparer.Ordinal).ToList(),
                Workspaces = data.Workspaces.OrderBy(w=>w.Name,StringComparer.Ordinal).ToList(), Machines = data.Machines.OrderBy(m=>m.Id,StringComparer.Ordinal).ToList(),
                CertificateProviders = data.CertificateProviders };
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new PortableSnapshot(2, document.RootElement, portable,
                hosts.OrderBy(h=>h.Host,StringComparer.Ordinal).ThenBy(h=>h.Port).ThenBy(h=>h.Algorithm,StringComparer.Ordinal).ToList()), Json);
            if (bytes.Length > MaxPlaintext) { CryptographicOperations.ZeroMemory(bytes); throw new InvalidDataException("Archivio oltre 32 MiB: la copia locale è conservata."); }
            return bytes;
        } finally { CryptographicOperations.ZeroMemory(records); }
    }
    public static byte[] Canonicalize(byte[] bytes)
    {
        var value=Parse(bytes);
        var records=value.Records.Deserialize<List<KeyRecord>>(Json)!;
        try {
            using var doc=JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(records.OrderBy(r=>r.Info.Id,StringComparer.Ordinal),Json));
            var data=value.Data;data.Settings=new();data.FileLocations=[];data.IntegrityObservations=[];
            data.ConnectionViews=data.ConnectionViews.OrderBy(view=>view.Id).ToList();data.ConnectionTemplates=data.ConnectionTemplates.OrderBy(template=>template.Id,StringComparer.Ordinal).ToList();data.Profiles=data.Profiles.OrderBy(p=>p.Id,StringComparer.Ordinal).ToList();data.Folders=data.Folders.Order(StringComparer.Ordinal).ToList();
            data.Snippets=data.Snippets.OrderBy(s=>s.Id,StringComparer.Ordinal).ToList();data.Workspaces=data.Workspaces.OrderBy(w=>w.Name,StringComparer.Ordinal).ToList();data.Machines=data.Machines.OrderBy(m=>m.Id,StringComparer.Ordinal).ToList();
            return JsonSerializer.SerializeToUtf8Bytes(value with {Version=2,Records=doc.RootElement,Data=data,HostKeys=value.HostKeys.OrderBy(h=>h.Host,StringComparer.Ordinal).ThenBy(h=>h.Port).ThenBy(h=>h.Algorithm,StringComparer.Ordinal).ToList()},Json);
        } finally {foreach(var record in records)CryptographicOperations.ZeroMemory(record.Content);}
    }
    public static PortableSnapshot Parse(byte[] bytes)
    {
        if (bytes.Length > MaxPlaintext) throw new InvalidDataException("Archivio troppo grande.");
        int applicationVersion;using(var probe=JsonDocument.Parse(bytes)){if(probe.RootElement.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("Archivio non valido.");var version=probe.RootElement.EnumerateObject().LastOrDefault(property=>property.Name.Equals("version",StringComparison.OrdinalIgnoreCase)).Value;if(version.ValueKind!=JsonValueKind.Number||!version.TryGetInt32(out applicationVersion)||applicationVersion is not(1 or 2))throw new InvalidDataException("Versione dell’archivio non supportata.");}
        var value = JsonSerializer.Deserialize<PortableSnapshot>(bytes, applicationVersion==2?Schema2Json:Json) ?? throw new InvalidDataException("Archivio non valido.");

        if(value.Version is not (1 or 2) || value.Data is null || value.HostKeys is null || value.Data.ConnectionTemplates is null || value.Data.ConnectionTemplates.Count>50 || value.Data.ConnectionTemplates.Any(template=>template is null) || value.Data.ConnectionTemplates.Select(template=>template.Id).Distinct(StringComparer.Ordinal).Count()!=value.Data.ConnectionTemplates.Count || value.Data.Profiles is null || value.Data.Folders is null || value.Data.Snippets is null || value.Data.Workspaces is null || value.Data.Machines is null || value.Data.CertificateProviders is null || value.Data.Profiles.Count > 10000 || value.Data.Profiles.Any(p=>p is null) || value.Data.Machines.Any(m=>m is null) || value.Data.Workspaces.Any(w=>w is null) || value.HostKeys.Any(h=>h is null) || value.Data.Profiles.Select(p=>p.Id).Distinct().Count()!=value.Data.Profiles.Count)
            throw new InvalidDataException("Profili online non validi.");
        if(value.Data.PortablePreferences is null)throw new InvalidDataException("Preferenze sincronizzate non valide.");value.Data.PortablePreferences.Validate();
        ConnectionSearchView.Validate(value.Data.ConnectionViews);
        foreach(var template in value.Data.ConnectionTemplates)template.Validate();
        foreach(var profile in value.Data.Profiles) profile.Validate();
        foreach(var machine in value.Data.Machines) machine.Validate();
        foreach(var workspace in value.Data.Workspaces) workspace.Validate();
        KeyVault.ValidateSyncRecords(value.Records,value.Version==2?Schema2Json:Json);
        if(value.HostKeys.Any(h=>string.IsNullOrWhiteSpace(h.Host)||h.Port is <1 or >65535||string.IsNullOrWhiteSpace(h.Fingerprint)))throw new InvalidDataException("Identità host non valide.");
        return value;
    }
}

public sealed partial class KeyVault
{
    private static bool PortableRecord(KeyRecord r) => r.Info.Kind is "ssh-key" or "ssh-native-key" or "password" or "ssh-certificate-revocation";
    public byte[] ExportSyncRecords()
    {
        lock (_gate) { if (_key is null) throw new InvalidOperationException("Sblocca prima l’archivio."); return JsonSerializer.SerializeToUtf8Bytes(_records.Where(PortableRecord).OrderBy(r=>r.Info.Id,StringComparer.Ordinal), VaultSyncCrypto.Json); }
    }
    public static void ValidateSyncRecords(JsonElement json,JsonSerializerOptions? options=null)
    {
        var records = json.Deserialize<List<KeyRecord>>(options??VaultSyncCrypto.Json) ?? throw new InvalidDataException("Chiavi non valide.");
        try {
            if (records.Count > 20000 || records.Any(r=>r is null||r.Info is null||r.Content is null) || records.Select(r=>r.Info.Id).Distinct().Count()!=records.Count || records.Any(r=>r.Info is null || !PortableRecord(r) || string.IsNullOrWhiteSpace(r.Info.Id) || r.Info.Id.Length>100 || r.Info.Name is null || r.Info.Name.Length>100 || r.Content is null || r.Content.Length>1024*1024))
                throw new InvalidDataException("Chiavi dell’archivio online non valide.");
            foreach(var record in records)ItemDiscussion.Validate(record.Info.Discussion);
        } finally { foreach(var record in records) if(record is not null && record.Content is not null) CryptographicOperations.ZeroMemory(record.Content); }
    }
    public byte[]? SyncKey(string owner)
    {
        lock (_gate) { if(_key is null) throw new InvalidOperationException("Sblocca prima l’archivio.");return _records.SingleOrDefault(r=>r.Info.Kind=="sync-key"&&r.Info.Comment==owner)?.Content.ToArray(); }
    }
    public void StoreSyncKey(string owner, byte[] key)
    {
        lock(_gate) {
            RequireUnlocked(); if(key.Length!=32)throw new ArgumentException("Chiave di sincronizzazione non valida.");
            var existing=_records.SingleOrDefault(r=>r.Info.Kind=="sync-key"&&r.Info.Comment==owner);
            var replacement=new KeyRecord(new KeyInfo(existing?.Info.Id??Guid.NewGuid().ToString("N"),"Sincronizzazione archivio",owner,DateTime.UtcNow,Kind:"sync-key"),key.ToArray());
            if(existing!=null)_records.Remove(existing);_records.Add(replacement);
            try { Save(); if(existing!=null)CryptographicOperations.ZeroMemory(existing.Content); }
            catch { _records.Remove(replacement);if(existing!=null)_records.Add(existing);CryptographicOperations.ZeroMemory(replacement.Content);throw; }
        }
    }
    public void ImportSyncRecords(JsonElement json)
    {
        ValidateSyncRecords(json);
        var incoming=json.Deserialize<List<KeyRecord>>(VaultSyncCrypto.Json)!;
        lock(_gate) {
            RequireUnlocked();var prior=_records;
            // Recording encryption and device-bound sync keys are never replaced by another device.
            var retainedRevocations=prior.Where(r=>r.Info.Kind=="ssh-certificate-revocation"&&!incoming.Any(n=>n.Info.Id==r.Info.Id)).ToList();
            _records=[..prior.Where(r=>!PortableRecord(r)),..retainedRevocations,..incoming];
            try { Save(); foreach(var record in prior.Where(r=>PortableRecord(r)&&!retainedRevocations.Contains(r)))CryptographicOperations.ZeroMemory(record.Content); }
            catch { _records=prior;foreach(var record in incoming)CryptographicOperations.ZeroMemory(record.Content);throw; }
        }
    }
}

public static class SyncLocalBackup
{
    private static readonly string[] Files = ["keys.vault", "profiles.json", "hostkeys.json", "account-sync.dpapi", "account-unlock.dpapi"];
    public static string Create(string directory, bool transaction)
    {
        var backup=Path.Combine(directory,"MigrationBackups",DateTime.UtcNow.ToString("yyyyMMddTHHmmss")+"-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backup);
        var existed=new List<string>();
        foreach(var name in Files)if(File.Exists(Path.Combine(directory,name))){File.Copy(Path.Combine(directory,name),Path.Combine(backup,name));existed.Add(name);}
        AtomicFile.Write(Path.Combine(backup,"manifest.json"),JsonSerializer.SerializeToUtf8Bytes(existed));
        if(transaction)AtomicFile.Write(Path.Combine(directory,"sync-transaction.json"),JsonSerializer.SerializeToUtf8Bytes(Path.GetFileName(backup)));
        return backup;
    }
    public static void Complete(string directory) => File.Delete(Path.Combine(directory,"sync-transaction.json"));
    public static void Recover(string directory)
    {
        var journal=Path.Combine(directory,"sync-transaction.json");if(!File.Exists(journal))return;
        var name=JsonSerializer.Deserialize<string>(File.ReadAllText(journal))!;
        if(string.IsNullOrEmpty(name)||name!=Path.GetFileName(name)||name.Contains(".."))throw new InvalidDataException("Ripristino della sincronizzazione non valido.");
        var backup=Path.Combine(directory,"MigrationBackups",name);
        var existed=JsonSerializer.Deserialize<List<string>>(File.ReadAllText(Path.Combine(backup,"manifest.json")))!;
        foreach(var item in existed)if(!Files.Contains(item))throw new InvalidDataException("Manifest di ripristino non valido.");
        // Validate all backup files before changing any current file.
        var contents=existed.ToDictionary(item=>item,item=>File.ReadAllBytes(Path.Combine(backup,item)));
        foreach(var file in Files)if(contents.TryGetValue(file,out var bytes))AtomicFile.Write(Path.Combine(directory,file),bytes);else File.Delete(Path.Combine(directory,file));
        File.Delete(journal);
    }
}
