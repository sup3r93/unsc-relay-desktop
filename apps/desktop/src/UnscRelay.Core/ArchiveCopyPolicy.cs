using System.Net.Http;
namespace UnscRelay.Core;
public static class ArchiveCopyPolicy
{
 public static bool NetworkUnavailable(Exception ex)=>ex is HttpRequestException h&&(h.StatusCode==null||(int)h.StatusCode>=500)||ex is OnlineAccessException o&&o.Status is {} status&&(int)status>=500||ex is TaskCanceledException;
}
