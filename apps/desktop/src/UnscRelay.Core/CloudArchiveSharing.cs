using System.Globalization;
using System.Text.Json;
using Duende.IdentityModel.OidcClient.Browser;
namespace UnscRelay.Core;
public sealed record ArchiveSharingReviewMember(string Account,string Email,string Access,string? ExpiresAt);
public sealed record ArchiveSharingReview(Guid ArchiveId,bool Suspended,bool RequestedSuspended,long Version,string Action,string Resource,string Intent,string[] Fields,ArchiveSharingReviewMember[] Members);
public sealed partial class CloudAccountClient
{
 public static void ValidateSharingReview(ArchiveSharingReview review,Guid id,bool suspended)
 {
  var action=suspended?"archive.sharing.pause":"archive.sharing.resume";
  if(review.ArchiveId!=id||id==Guid.Empty||review.RequestedSuspended!=suspended||review.Action!=action||review.Resource!=id.ToString("D")||review.Version<0||review.Fields is null||review.Members is null||review.Members.Length>1000)throw new InvalidDataException("Riepilogo degli accessi non valido.");
  var fields=new List<string>{action,id.ToString("D"),review.Version.ToString(CultureInfo.InvariantCulture),review.Suspended?"true":"false",suspended?"true":"false"};
  foreach(var member in review.Members){if(member.Account is not{Length:64}||!member.Account.All(char.IsAsciiHexDigit)||member.Email is null||member.Email.Length>254||member.Access is not("read" or "write")||member.ExpiresAt!=null&&!DateTimeOffset.TryParse(member.ExpiresAt,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out _))throw new InvalidDataException("Riepilogo degli accessi non valido.");fields.Add(member.Account);fields.Add(member.Access);fields.Add(member.ExpiresAt??"");}
  if(!review.Fields.SequenceEqual(fields)||review.Intent!=SensitiveIntentHash(review.Fields))throw new InvalidDataException("Riepilogo degli accessi non valido.");
 }
 public async Task<ArchiveSharingReview> ReviewArchiveSharing(Guid id,bool suspended,CancellationToken ct)
 {var review=await ArchiveRequest<ArchiveSharingReview>(HttpMethod.Get,ArchivePath(id)+"/sharing?suspended="+(suspended?"true":"false"),null,ct);ValidateSharingReview(review,id,suspended);return review;}
 public async Task SetArchiveSharing(ArchiveSharingReview review,IBrowser browser,Action verifySession,CancellationToken ct)
 {ValidateSharingReview(review,review.ArchiveId,review.RequestedSuspended);var proof=await ConfirmSensitive(review.Action,review.Resource,review.Intent,browser,ct);verifySession();using var response=await Send(HttpMethod.Post,ArchivePath(review.ArchiveId)+"/sharing",new{suspended=review.RequestedSuspended,intent=review.Intent},ct,confirmation:proof);await PortalResult(response,ct);}
}
