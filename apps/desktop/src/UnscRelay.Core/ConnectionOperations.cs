namespace UnscRelay.Core;
public sealed record OperationalTask(string Id,string Text,bool Completed=false);
public sealed record ConnectionOperations
{
 public string Contact {get;init;}="";
 public string Maintenance {get;init;}="";
 public List<OperationalTask> Checklist {get;init;}=[];
 public void Validate()
 {
  if(Contact==null||Contact.Length>1000||Maintenance==null||Maintenance.Length>8000||Checklist==null||Checklist.Count>30)throw new ArgumentException("Note operative troppo lunghe o checklist non valida.");
  var ids=new HashSet<Guid>();foreach(var item in Checklist)if(item==null||!Guid.TryParse(item.Id,out var id)||id==Guid.Empty||!ids.Add(id)||string.IsNullOrWhiteSpace(item.Text)||item.Text.Length>300||item.Text.Any(c=>char.IsControl(c)))throw new ArgumentException("Ogni attività richiede un identificatore univoco e un testo di massimo 300 caratteri.");
 }
}
