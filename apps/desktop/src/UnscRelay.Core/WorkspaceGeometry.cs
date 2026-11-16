namespace UnscRelay.Core;
public sealed record WorkspaceWindow(string Monitor,double X,double Y,double Width,double Height,bool Maximized=false,int Order=0)
{
    public void Validate()
    {
        if(string.IsNullOrWhiteSpace(Monitor)||Monitor.Length>256||Monitor.Any(char.IsControl)||!new[]{X,Y,Width,Height}.All(double.IsFinite)||X is <0 or >1||Y is <0 or >1||Width is <0.01 or >1||Height is <0.01 or >1||Order is <0 or >9)throw new ArgumentException("Posizione della finestra workspace non valida.");
    }
}
public sealed record MonitorArea(string Id,int Left,int Top,int Width,int Height,bool Primary=false);
public sealed record WindowArea(int Left,int Top,int Width,int Height);
public static class WorkspaceGeometry
{
    static void Check(MonitorArea monitor){if(string.IsNullOrWhiteSpace(monitor.Id)||monitor.Width is <1 or >100000||monitor.Height is <1 or >100000||Math.Abs((long)monitor.Left)>100000||Math.Abs((long)monitor.Top)>100000)throw new ArgumentException("Area monitor non valida.");}
    public static WorkspaceWindow Capture(MonitorArea monitor,WindowArea rect,bool maximized,int order)
    {
        Check(monitor);var width=Math.Clamp(rect.Width,Math.Min(100,monitor.Width),monitor.Width);var height=Math.Clamp(rect.Height,Math.Min(100,monitor.Height),monitor.Height);
        var x=Math.Clamp(rect.Left-monitor.Left,0,monitor.Width-width);var y=Math.Clamp(rect.Top-monitor.Top,0,monitor.Height-height);
        var result=new WorkspaceWindow(monitor.Id,(double)x/monitor.Width,(double)y/monitor.Height,(double)width/monitor.Width,(double)height/monitor.Height,maximized,order);result.Validate();return result;
    }
    public static (MonitorArea Monitor,WindowArea Bounds,bool Relocated) Resolve(WorkspaceWindow saved,IReadOnlyList<MonitorArea> monitors,int minWidth=850,int minHeight=580)
    {
        saved.Validate();if(monitors.Count is <1 or >64||minWidth<1||minHeight<1)throw new ArgumentException("Monitor o dimensioni non validi.");foreach(var monitor in monitors)Check(monitor);
        var target=monitors.FirstOrDefault(m=>m.Id.Equals(saved.Monitor,StringComparison.OrdinalIgnoreCase));var relocated=target==null;target??=monitors.FirstOrDefault(m=>m.Primary)??monitors[0];
        int width=Math.Clamp((int)Math.Round(saved.Width*target.Width),Math.Min(minWidth,target.Width),target.Width),height=Math.Clamp((int)Math.Round(saved.Height*target.Height),Math.Min(minHeight,target.Height),target.Height);
        var left=target.Left+Math.Clamp((int)Math.Round(saved.X*target.Width),0,target.Width-width);var top=target.Top+Math.Clamp((int)Math.Round(saved.Y*target.Height),0,target.Height-height);
        return(target,new(left,top,width,height),relocated);
    }
}
