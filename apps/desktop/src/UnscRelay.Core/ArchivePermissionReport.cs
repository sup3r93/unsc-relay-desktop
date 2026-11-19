namespace UnscRelay.Core;
public static class ArchivePermissionReport
{
 public static string Cell(string value){if(value.TrimStart() is{Length:>0} text&&text[0] is '=' or '+' or '-' or '@')value="'"+value;return "\""+value.Replace("\"","\"\"",StringComparison.Ordinal)+"\"";}
 public static string Create(SharedArchive archive,IEnumerable<SharedArchiveMember> members)
 {
  ArchiveDescriptorRules.Validate(archive);if(archive.Access!="owner"||archive.Offline)throw new InvalidOperationException("Il riepilogo richiede l’accesso online del titolare.");
  List<string[]> rows=[["Archive",archive.Name],["Sharing",archive.SharingSuspended?"Paused":"Active"],["Account","Permission","Expires at (UTC)"]];
  foreach(var member in members.Take(1001)){if(rows.Count>=1003)throw new InvalidDataException("Troppi accessi nel riepilogo.");rows.Add([member.Email.Length>0?member.Email:member.Account,member.Account==archive.OwnerAccount?"Owner":member.Access=="read"?"Read only":"Read and write",member.ExpiresAt?.ToUniversalTime().ToString("O")??"No expiry"]);}
  return "\ufeff"+string.Join("\r\n",rows.Select(row=>string.Join(',',row.Select(Cell))))+"\r\n";
 }
 public static string Html(SharedArchive archive,IEnumerable<SharedArchiveMember> members,string language,Func<string,string> text)
 {
  ArchiveDescriptorRules.Validate(archive);if(archive.Access!="owner"||archive.Offline)throw new InvalidOperationException("Il riepilogo richiede l’accesso online del titolare.");
  if(language is not("it" or "en" or "fr" or "de" or "es"))throw new ArgumentException("Lingua non valida.");
  string E(string value)=>System.Net.WebUtility.HtmlEncode(value);var rows=members.Take(1001).ToArray();if(rows.Length>1000)throw new InvalidDataException("Troppi accessi nel riepilogo.");
  var html=new System.Text.StringBuilder("<!doctype html><html lang=\""+language+"\"><head><meta charset=\"utf-8\"><meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'\"><title>"+E(text("Riepilogo dei permessi"))+"</title><style>@page{size:A4;margin:18mm}body{font:11pt 'Segoe UI',sans-serif;color:#17261e;line-height:1.5}header{border-bottom:3px solid #385743;padding-bottom:16px}small{letter-spacing:.12em}h1{font-size:25pt;margin:.5em 0}h2{font-size:17pt;overflow-wrap:anywhere}table{width:100%;border-collapse:collapse;table-layout:fixed;margin-top:24px}caption{text-align:left;font-weight:700;margin-bottom:10px}th,td{text-align:left;vertical-align:top;border-bottom:1px solid #a4afa8;padding:10px 6px;overflow-wrap:anywhere}th:first-child{width:48%}thead{display:table-header-group}tr{break-inside:avoid}footer{margin-top:24px;font-size:10pt;color:#35453b}</style></head><body><header><small>UNSC RELAY</small><h1>"+E(text("Riepilogo dei permessi"))+"</h1><h2>"+E(archive.Name)+"</h2></header><p>"+E(text("Condivisione"))+": "+E(text(archive.SharingSuspended?"Sospesa":"Attiva"))+"</p><p>"+E(text("Generato il"))+": "+DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'")+"</p><table><caption>"+E(text("Accessi all’archivio"))+"</caption><thead><tr><th scope=\"col\">"+E(text("Email"))+"</th><th scope=\"col\">"+E(text("Permessi"))+"</th><th scope=\"col\">"+E(text("Scadenza dell’accesso"))+"</th></tr></thead><tbody>");
  foreach(var member in rows)html.Append("<tr><td>"+E(member.Email.Length>0?member.Email:member.Account)+"</td><td>"+E(text(member.Account==archive.OwnerAccount?"Titolare":member.Access=="read"?"Sola lettura":"Lettura e scrittura"))+"</td><td>"+E(member.ExpiresAt?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'")??text("Senza scadenza"))+"</td></tr>");
  return html.Append("</tbody></table><footer>"+E(text("Il rapporto contiene gli accessi attuali, senza contenuti o chiavi. Conserva il file in un luogo riservato."))+"</footer></body></html>").ToString();
 }
}
