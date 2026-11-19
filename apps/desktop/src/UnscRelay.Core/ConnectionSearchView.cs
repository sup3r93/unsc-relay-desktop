namespace UnscRelay.Core;
public sealed record ConnectionSearchView(Guid Id,string Name,string Query)
{
 public static void Validate(IReadOnlyCollection<ConnectionSearchView>? views)
 {
  if(views is null||views.Count>30||views.Any(view=>view is null)||views.Select(view=>view.Id).Distinct().Count()!=views.Count)throw new InvalidDataException("Massimo 30 ricerche salvate con identificatori univoci.");var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var view in views)if(view.Id==Guid.Empty||string.IsNullOrWhiteSpace(view.Name)||view.Name.Length>80||view.Name.Any(char.IsControl)||!names.Add(view.Name.Trim())||view.Query is null||view.Query.Length>200||view.Query.Any(char.IsControl))throw new InvalidDataException("Ricerca salvata non valida.");
 }
}
