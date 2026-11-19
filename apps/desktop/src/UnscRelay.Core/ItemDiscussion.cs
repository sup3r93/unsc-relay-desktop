using System.Text.Json.Serialization;
namespace UnscRelay.Core;
public sealed record ItemComment(string Id,string Author,string Text,DateTimeOffset Created);
public static class ItemDiscussion
{
 public static void Validate(IReadOnlyList<ItemComment>? comments)
 {
  if(comments is null)return;
  if(comments.Count>100||comments.Any(value=>value is null)||comments.Select(value=>value.Id).Distinct(StringComparer.Ordinal).Count()!=comments.Count)throw new ArgumentException("Discussione non valida.");
  foreach(var value in comments)if(!Guid.TryParseExact(value.Id,"D",out _)||string.IsNullOrWhiteSpace(value.Author)||value.Author.Length>100||value.Author.Any(char.IsControl)||string.IsNullOrWhiteSpace(value.Text)||value.Text.Length>4000||value.Text.Any(character=>char.IsControl(character)&&character is not('\n' or '\r' or '\t'))||value.Created.UtcDateTime.Year is <2000 or >2100)throw new ArgumentException("Commento non valido.");
 }
 public static List<ItemComment> Append(IReadOnlyList<ItemComment>? current,string author,string text,DateTimeOffset now)
 {
  Validate(current);var next=current?.ToList()??[];next.Add(new(Guid.NewGuid().ToString("D"),author.Trim(),text.Trim(),now));Validate(next);return next;
 }
}
