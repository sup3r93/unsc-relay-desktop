using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.Browser;

namespace UnscRelay.Core;

public sealed partial class CloudAccountClient
{
 public static string SensitiveIntentHash(params string[] fields)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(fields.Select(value=>value.Length.ToString(CultureInfo.InvariantCulture)+":"+value)))));
 async Task<Guid> ConfirmSensitive(string action,string resource,string intent,IBrowser browser,CancellationToken ct)
 {
  using var create=await Send(HttpMethod.Post,"api/security-challenges",new{action,resource,intent},ct);var challenge=await PortalResult(create,ct);var id=challenge.GetProperty("id").GetGuid();var nonce=challenge.GetProperty("nonce").GetString()!;
  if(nonce.Length!=64||!nonce.All(char.IsAsciiHexDigit))throw new OnlineAccessException("Richiesta di verifica non valida.");
  string identityToken;
  await authGate.WaitAsync(ct);
  try{
   var request=new LoginRequest();request.FrontChannelExtraParameters.Add("ui_locales",UiLanguage);request.FrontChannelExtraParameters.Add("nonce",nonce);request.FrontChannelExtraParameters.Add("acr_values","unsc-mfa");request.FrontChannelExtraParameters.Add("max_age","0");request.FrontChannelExtraParameters.Add("prompt","login");var hint=RememberedEmail(State);if(hint.Length>0)request.FrontChannelExtraParameters.Add("login_hint",hint);
   var result=await Client(browser,"unsc-desktop-stepup").LoginAsync(request,ct);
   if(result.IsError||string.IsNullOrWhiteSpace(result.IdentityToken))throw new OnlineAccessException("Conferma dell’identità non completata. Nessuna modifica applicata.");
   // The server checks signature, audience, subject, nonce, fresh ACR/AMR and one-time use.
   // Identity tokens stay inside the native process and are never persisted or sent to the WebView.
   identityToken=result.IdentityToken;
  }finally{authGate.Release();}
  using var verify=await Send(HttpMethod.Post,"api/security-challenges/"+id.ToString("D")+"/verify",new{identityToken},ct);await PortalResult(verify,ct);return id;
 }
 public async Task RenameSecurityMethod(string id,string name,IBrowser browser,CancellationToken ct)
 {
  CheckCredentialId(id);name=name.Trim();if(name.Length is <1 or >80||name.Contains('\0'))throw new ArgumentException("Usa un nome da 1 a 80 caratteri.");
  var proof=await ConfirmSensitive("credential.rename",id,SensitiveIntentHash("credential.rename",id,name),browser,ct);
  using var response=await Send(HttpMethod.Put,"api/security-methods/"+Uri.EscapeDataString(id)+"/name",new{name},ct,confirmation:proof);await PortalResult(response,ct);
 }
 public async Task RemoveSecurityMethod(string id,IBrowser browser,CancellationToken ct)
 {
  CheckCredentialId(id);var proof=await ConfirmSensitive("credential.remove",id,SensitiveIntentHash("credential.remove",id),browser,ct);
  using var response=await Send(HttpMethod.Delete,"api/security-methods/"+Uri.EscapeDataString(id),null,ct,confirmation:proof);await PortalResult(response,ct);
 }
 public async Task<JsonElement> UpdatePersonalProfile(string firstName,string lastName,string email,IBrowser browser,CancellationToken ct)
 {
  firstName=firstName.Trim();lastName=lastName.Trim();email=email.Trim().ToLowerInvariant();
  if(firstName.Length>100||lastName.Length>100||firstName.Any(char.IsControl)||lastName.Any(char.IsControl)||email.Length>254||!System.Net.Mail.MailAddress.TryCreate(email,out var parsed)||parsed.Address!=email)throw new ArgumentException("Controlla nome, cognome ed email.");
  var proof=await ConfirmSensitive("profile.update","self",SensitiveIntentHash("profile.update","self",firstName,lastName,email),browser,ct);
  using var response=await Send(HttpMethod.Put,"api/profile",new{firstName,lastName,email},ct,confirmation:proof);var result=await PortalResult(response,ct);
  State.Email=email;State.Name=string.Join(" ",new[]{firstName,lastName}.Where(value=>value.Length>0));if(State.Name.Length==0)State.Name=email;
  if(result.TryGetProperty("reauthenticate",out var expired)&&expired.GetBoolean())ClearOnlineTokens();Save();return result;
 }
 public async Task<JsonElement> UpdatePersonalPassword(string password,IBrowser browser,CancellationToken ct)
 {
  if(password.Length is <12 or >1024||password.Any(char.IsControl))throw new ArgumentException("Usa una password da 12 a 1024 caratteri, senza caratteri di controllo.");
  var proof=await ConfirmSensitive("password.update","self",SensitiveIntentHash("password.update","self",password),browser,ct);
  using var response=await Send(HttpMethod.Put,"api/profile/password",new{password},ct,confirmation:proof);var result=await PortalResult(response,ct);ClearOnlineTokens();Save();return result;
 }
 void ClearOnlineTokens(){State.AccessToken="";State.RefreshToken="";State.TokenExpires=DateTimeOffset.MinValue;}
 static void CheckCredentialId(string id){if(id.Length is <1 or >128||id.Any(char.IsControl))throw new ArgumentException("Metodo di sicurezza non valido.");}
}
