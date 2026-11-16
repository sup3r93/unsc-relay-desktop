namespace UnscRelay.Core;
public static class TransferWindow
{
 public static void Validate(int? start,int? end){if(start is null&&end is null)return;if(start is null or <0 or >1439||end is null or <0 or >1439||start==end)throw new ArgumentException("Indica inizio e fine diversi per la fascia di trasferimento.");}
 public static bool Allowed(int minute,int? start,int? end){Validate(start,end);if(start==null)return true;return start<end?minute>=start&&minute<end:minute>=start||minute<end;}
 public static void Wait(int? start,int? end,CancellationToken cancellation){while(!Allowed(DateTime.Now.Hour*60+DateTime.Now.Minute,start,end)){if(cancellation.WaitHandle.WaitOne(TimeSpan.FromSeconds(1)))cancellation.ThrowIfCancellationRequested();}}
}
