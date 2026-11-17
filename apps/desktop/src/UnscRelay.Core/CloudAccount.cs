using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.Browser;

namespace UnscRelay.Core;

public sealed class AccountSyncState
{
    public string Format { get; set; } = "unsc-account-v1";
    public string Owner { get; set; } = "";
    public string ArchiveOwner { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Subject { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTimeOffset TokenExpires { get; set; }
    public bool Activated { get; set; }
    public bool Migrated { get; set; }
    public long Revision { get; set; }
    public string Baseline { get; set; } = "";
    public SyncEnvelope? Header { get; set; }
}
public sealed record RemoteVault(long Revision, DateTime UpdatedAt, SyncEnvelope Envelope);
public sealed record OnlineIdentity(string Owner, string Subject, string Name, string Email="");
public class OnlineAccessException(string message,HttpStatusCode? status=null) : Exception(message){public HttpStatusCode? Status{get;}=status;}

public sealed class PersonalArchiveChangedException():OnlineAccessException("L’archivio personale sincronizzato è cambiato. Apri Account e sincronizzazione e usa il recupero dalla copia locale verificata. Nessun dato viene sostituito automaticamente.");

public sealed class AccountSyncStore(string directory)
{
    private string FilePath => Path.Combine(directory,"account-sync.dpapi");
    public AccountSyncState Load()
    {
        if(!File.Exists(FilePath))return new();
        if(new FileInfo(FilePath).Length>256*1024)throw new InvalidDataException("Collegamento account non valido. La copia locale non è stata modificata.");
        byte[] bytes;
        try{bytes=UpdateTokenStore.Transform(File.ReadAllBytes(FilePath),true);}
        catch(InvalidOperationException ex){throw new InvalidDataException("Il collegamento online non è leggibile su questo account Windows. I file locali restano intatti.",ex);}
        try {
            var state=JsonSerializer.Deserialize<AccountSyncState>(bytes,VaultSyncCrypto.Json)??throw new InvalidDataException("Account non valido.");
            if(state.Format!="unsc-account-v1" || state.Activated&&(state.Owner.Length!=64||state.Owner.Any(c=>!char.IsAsciiHexDigit(c))||string.IsNullOrWhiteSpace(state.Subject)))throw new InvalidDataException("Collegamento account non valido.");
            if(state.ArchiveOwner.Length>0&&(state.ArchiveOwner.Length!=64||!state.ArchiveOwner.All(char.IsAsciiHexDigit)))throw new InvalidDataException("Namespace archivio non valido.");
            return state;
        }finally{CryptographicOperations.ZeroMemory(bytes);}
    }
    public void Save(AccountSyncState state)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(state,VaultSyncCrypto.Json);
        try { AtomicFile.Write(FilePath,UpdateTokenStore.Transform(bytes,false)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}

public sealed partial class CloudAccountClient : IDisposable
{
    public const string Authority="https://unsc.responda.eu/identity/realms/unsc";
    public const string Callback="http://127.0.0.1:48187/relay-callback/";
    private readonly AccountSyncStore storage;
    private readonly SemaphoreSlim authGate=new(1,1);
    private readonly HttpClient http;
    public AccountSyncState State { get; }
    public CloudAccountClient(string directory) : this(directory,new HttpClientHandler { AllowAutoRedirect=false }) { }
    internal CloudAccountClient(string directory,HttpMessageHandler handler) { storage=new(directory);State=storage.Load();http=new(handler){BaseAddress=new Uri("https://unsc.responda.eu/"),Timeout=TimeSpan.FromSeconds(30),MaxResponseContentBufferSize=64*1024*1024}; }
    private static OidcClient Client(IBrowser? browser=null)=>new(new OidcClientOptions { Authority=Authority,ClientId="unsc-desktop",RedirectUri=Callback,Scope="openid profile email",Browser=browser,LoadProfile=false });
    public void Save()=>storage.Save(State);
    public static string RememberedEmail(AccountSyncState state)
    {
        foreach(var value in new[]{state.Email,state.Name})
            if(value is {Length:>0 and <=254}&&!value.Any(char.IsControl)&&System.Net.Mail.MailAddress.TryCreate(value,out var address)&&address.Address.Equals(value,StringComparison.OrdinalIgnoreCase))return value;
        return "";
    }
    public async Task LoginAsync(CancellationToken ct, IBrowser? browser=null, string action="login")
    {
        await authGate.WaitAsync(ct);
        try {
            if(action=="register"&&!await RegistrationAllowed(ct))throw new OnlineAccessException("Le registrazioni sono temporaneamente chiuse dall’amministratore.");
            var request=new LoginRequest();
            var hint=RememberedEmail(State);if(action!="register"&&hint.Length>0)request.FrontChannelExtraParameters.Add("login_hint",hint);
            request.FrontChannelExtraParameters.Add("prompt",action=="register"?"create":"login");
            if(action=="login")request.FrontChannelExtraParameters.Add("max_age","0");
            if(action is not("login" or "register")){
                var required=DesktopAccountNavigation.Action(action)??throw new ArgumentException("Operazione account non valida.");
                request.FrontChannelExtraParameters.Add("kc_action",required);request.FrontChannelExtraParameters.Add("max_age","0");
            }
            var login=await Client(browser??new LoopbackAccountBrowser()).LoginAsync(request,ct);
            if(login.IsError||string.IsNullOrEmpty(login.AccessToken))throw new OnlineAccessException("Accesso al sito non completato. Nessun dato locale è stato modificato.");
            var identity=await Identity(login.AccessToken,ct);
            if(State.Owner.Length>0&&identity.Owner!=State.Owner)throw new OnlineAccessException("Questo archivio locale è già associato a un altro account. Accedi con l’account originale: non mescolerò i suoi dati.");
            State.Owner=identity.Owner;State.Subject=identity.Subject;State.Name=identity.Name;
            State.Email=RememberedEmail(new AccountSyncState { Email=identity.Email,Name=identity.Name });
            State.AccessToken=login.AccessToken;State.RefreshToken=login.RefreshToken??"";State.TokenExpires=login.AccessTokenExpiration;State.Activated=true;Save();
        }finally{authGate.Release();}
    }
    public async Task<bool> RegistrationAllowed(CancellationToken ct)
    {
        using var response=await http.GetAsync("api/desktop/availability",ct);response.EnsureSuccessStatusCode();
        using var document=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));return document.RootElement.GetProperty("registrationAllowed").GetBoolean();
    }
    public async Task<JsonElement> Profile(CancellationToken ct)
    {
        using var response=await Send(HttpMethod.Get,"api/profile",null,ct);response.EnsureSuccessStatusCode();
        using var document=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));return document.RootElement.Clone();
    }
    public async Task VerifyOnline(CancellationToken ct){var identity=await Identity(await Token(ct),ct);if(identity.Owner!=State.Owner)throw new OnlineAccessException("Identità online diversa dall’archivio locale.");}
    public async Task LogoutAsync(CancellationToken ct)
    {
        await authGate.WaitAsync(ct);
        try
        {
            var refresh=State.RefreshToken;
            State.AccessToken="";State.RefreshToken="";State.TokenExpires=DateTimeOffset.MinValue;State.Activated=false;
            // An archive never uploaded may be linked to the correct personal account after explicit logout.
            if(!State.Migrated&&State.Header is null&&State.Revision==0){State.Owner="";State.Subject="";State.Name="";State.Baseline="";}
            Save();
            if(refresh.Length==0)return;
            try{using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(8));
                using var content=new FormUrlEncodedContent(new Dictionary<string,string>{{"client_id","unsc-desktop"},{"token_type_hint","refresh_token"},{"token",refresh}});
                using var response=await http.PostAsync(Authority+"/protocol/openid-connect/revoke",content,timeout.Token);
            }catch(Exception e)when(e is HttpRequestException or OperationCanceledException){} // Local logout already completed, including offline.
        }finally{authGate.Release();}
    }
    private async Task<OnlineIdentity> Identity(string token,CancellationToken ct)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,"api/account");request.Headers.Authorization=new("Bearer",token);
        using var response=await http.SendAsync(request,ct);
        if(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)throw new OnlineAccessException("Account non abilitato per l’app. Gli amministratori usano soltanto il sito; per Relay scegli un account personale abilitato.");
        response.EnsureSuccessStatusCode();
        var identity=await response.Content.ReadFromJsonAsync<OnlineIdentity>(VaultSyncCrypto.Json,ct)??throw new OnlineAccessException("Identità online non valida.");
        if(identity.Owner.Length!=64||identity.Owner.Any(c=>!char.IsAsciiHexDigit(c))||string.IsNullOrWhiteSpace(identity.Subject))throw new OnlineAccessException("Identità online non valida.");
        return identity;
    }
    private async Task<string> Token(CancellationToken ct)
    {
        await authGate.WaitAsync(ct);
        try {
            if(!State.Activated)throw new OnlineAccessException("Collega prima l’account UNSC.");
            if(State.AccessToken.Length>0&&State.TokenExpires>DateTimeOffset.UtcNow.AddSeconds(30))return State.AccessToken;
            if(State.RefreshToken.Length==0)throw new OnlineAccessException("Accedi di nuovo al sito per riprendere la sincronizzazione. L’app può essere usata offline.");
            var refreshed=await Client().RefreshTokenAsync(State.RefreshToken,cancellationToken:ct);
            if(refreshed.IsError)throw new OnlineAccessException("La sessione online è scaduta. Accedi di nuovo; la copia offline resta disponibile.");
            var identity=await Identity(refreshed.AccessToken,ct);
            if(identity.Owner!=State.Owner)throw new OnlineAccessException("L’identità online è cambiata. Sincronizzazione bloccata.");
            State.AccessToken=refreshed.AccessToken;State.RefreshToken=string.IsNullOrEmpty(refreshed.RefreshToken)?State.RefreshToken:refreshed.RefreshToken;
            State.TokenExpires=refreshed.AccessTokenExpiration;Save();return State.AccessToken;
        }finally{authGate.Release();}
    }
    private async Task<HttpResponseMessage> Send(HttpMethod method,string path,object? value,CancellationToken ct,long? expected=null)
    {
        for(var attempt=0;attempt<2;attempt++) {
            using var request=new HttpRequestMessage(method,path);request.Headers.Authorization=new("Bearer",await Token(ct));
            if(value is not null)request.Content=JsonContent.Create(value,options:VaultSyncCrypto.Json);
            if(method==HttpMethod.Put)request.Headers.IfMatch.Add(new EntityTagHeaderValue("\""+(expected??State.Revision)+"\""));
            var response=await http.SendAsync(request,ct);
            if(response.StatusCode==HttpStatusCode.Unauthorized&&attempt==0){response.Dispose();State.TokenExpires=DateTimeOffset.MinValue;continue;}
            if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden){response.Dispose();throw new OnlineAccessException("Accesso online non disponibile. Accedi di nuovo; la copia locale è conservata.",response.StatusCode);}
            return response;
        }
        throw new OnlineAccessException("Accesso online non disponibile.");
    }
    public async Task<byte[]> DeviceUnlock(Guid device,bool create,CancellationToken ct)
    {
        if(device==Guid.Empty)throw new ArgumentException("Dispositivo non valido.",nameof(device));
        using var response=await Send(create?HttpMethod.Post:HttpMethod.Get,"api/desktop/unlock/"+device.ToString("D"),create?new{}:null,ct);
        if(response.StatusCode==HttpStatusCode.NotFound)throw new OnlineAccessException(create
            ? "Servizio di sblocco online non disponibile o non aggiornato. Contatta l’amministratore; l’archivio locale è conservato."
            : "Collegamento di sicurezza del dispositivo non trovato sul server. L’archivio locale è conservato e non è stato ricollegato automaticamente.");
        if((int)response.StatusCode>=500)throw new OnlineAccessException("Servizio di sblocco temporaneamente non disponibile. Riprova più tardi; l’archivio locale è conservato.");
        response.EnsureSuccessStatusCode();
        try
        {
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var bytes=Convert.FromBase64String(json.RootElement.GetProperty("secret").GetString()!);
            if(bytes.Length!=32){CryptographicOperations.ZeroMemory(bytes);throw new CryptographicException("Risposta di sblocco non valida.");}
            return bytes;
        }
        catch(Exception ex)when(ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentNullException)
        { throw new CryptographicException("Risposta di sblocco non valida. L’archivio locale è conservato.",ex); }
    }
    public string SyncOwner=>State.ArchiveOwner.Length>0?State.ArchiveOwner:State.Owner;
    public async Task<RemoteVault?> Read(CancellationToken ct)
    {
        using var response=await Send(HttpMethod.Get,"api/vault",null,ct);
        if(response.StatusCode==HttpStatusCode.NotFound)return null;
        response.EnsureSuccessStatusCode();
        // v1 legacy file packages must never be silently replaced by a structured vault.
        var json=await response.Content.ReadAsStringAsync(ct);using var doc=JsonDocument.Parse(json);
        if(doc.RootElement.GetProperty("envelope").GetProperty("version").GetInt32()!=2)throw new OnlineAccessException("Il sito contiene un vecchio pacchetto: scaricalo e conservalo prima di migrare l’archivio.");
        var remote=JsonSerializer.Deserialize<RemoteVault>(json,VaultSyncCrypto.Json)!;
        if(remote.Revision<1)throw new InvalidDataException("Revisione online non valida.");
        if(remote.Envelope.Owner!=SyncOwner)throw new PersonalArchiveChangedException();
        VaultSyncCrypto.Validate(remote.Envelope,SyncOwner);return remote;
    }
    public async Task<long?> Write(SyncEnvelope envelope,CancellationToken ct)
    {
        using var response=await Send(HttpMethod.Put,"api/vault",envelope,ct);
        if(response.StatusCode==HttpStatusCode.Conflict)return null;
        response.EnsureSuccessStatusCode();using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return json.RootElement.GetProperty("revision").GetInt64();
    }
    public void Dispose(){http.Dispose();authGate.Dispose();}
}

public sealed class LoopbackAccountBrowser : IBrowser
{
    public async Task<BrowserResult> InvokeAsync(BrowserOptions options,CancellationToken cancellationToken=default)
    {
        var start=new Uri(options.StartUrl);
        if(start.Scheme!="https"||start.Host!="unsc.responda.eu"||options.EndUrl!=CloudAccountClient.Callback)throw new InvalidOperationException("Destinazione di accesso non autorizzata.");
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);timeout.CancelAfter(TimeSpan.FromMinutes(10));
        var listener=new TcpListener(IPAddress.Loopback,48187);listener.Server.ExclusiveAddressUse=true;
        try {
            listener.Start(4);
            Process.Start(new ProcessStartInfo(options.StartUrl){UseShellExecute=true});
            while(true) {
                using var connection=await listener.AcceptTcpClientAsync(timeout.Token);
                using var stream=connection.GetStream();using var readTimeout=CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);readTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                var bytes=new byte[16384];var length=0;string header="";
                while(length<bytes.Length){var count=await stream.ReadAsync(bytes.AsMemory(length),readTimeout.Token);if(count==0)break;length+=count;header=Encoding.ASCII.GetString(bytes,0,length);if(header.Contains("\r\n\r\n"))break;}
                var lines=header.Split("\r\n");var parts=lines[0].Split(' ');
                if(parts.Length!=3||parts[0]!="GET"||!parts[1].StartsWith("/relay-callback/?",StringComparison.Ordinal)||!lines.Any(line=>line.Equals("Host: 127.0.0.1:48187",StringComparison.OrdinalIgnoreCase)))continue;
                var body=Encoding.UTF8.GetBytes("<!doctype html><meta charset=utf-8><title>UNSC Relay</title><p>Puoi tornare a UNSC Relay. Questa scheda può essere chiusa.</p>");
                var response=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nCache-Control: no-store\r\nReferrer-Policy: no-referrer\r\nContent-Security-Policy: default-src 'none'\r\nConnection: close\r\nContent-Length: "+body.Length+"\r\n\r\n");
                await stream.WriteAsync(response,timeout.Token);await stream.WriteAsync(body,timeout.Token);
                return new BrowserResult { ResultType=BrowserResultType.Success,Response=CloudAccountClient.Callback.TrimEnd('/')+"/"+parts[1]["/relay-callback/".Length..] };
            }
        }catch(OperationCanceledException){return new BrowserResult {ResultType=BrowserResultType.UserCancel,Error="Accesso annullato."};}
        finally{listener.Stop();}
    }
}
