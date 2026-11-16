using System.Security.Cryptography;
using System.Text;

namespace UnscRelay.Core;
public sealed partial class KeyVault
{
    public string StorePassword(string name, string password, string id = "")
    {
        lock (_gate)
        {
            RequireUnlocked();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || password.Length is < 1 or > 8192 || password.Contains('\0')) throw new ArgumentException("Nome o password non validi.");
            var prior = id.Length > 0 ? _records.Single(k => k.Info.Id == id && k.Info.Kind == "password") : null;
            var info = prior != null ? prior.Info with { Name = name } : new KeyInfo(Guid.NewGuid().ToString("N"), name, "Password connessione", DateTime.UtcNow, Kind: "password");
            var replacement = new KeyRecord(info, Encoding.UTF8.GetBytes(password));
            if (prior != null) _records.Remove(prior); _records.Add(replacement);
            try { Save(); if (prior != null) CryptographicOperations.ZeroMemory(prior.Content); return info.Id; }
            catch { _records.Remove(replacement); CryptographicOperations.ZeroMemory(replacement.Content); if (prior != null) _records.Add(prior); throw; }
        }
    }
    public string ReadPassword(string id)
    {
        lock (_gate) { RequireUnlocked(); return Encoding.UTF8.GetString(_records.Single(k => k.Info.Id == id && k.Info.Kind == "password").Content); }
    }
    public IReadOnlyList<KeyInfo> Passwords() { lock (_gate) { RequireUnlocked(); return _records.Where(k => k.Info.Kind == "password").Select(k => k.Info).ToArray(); } }
}
