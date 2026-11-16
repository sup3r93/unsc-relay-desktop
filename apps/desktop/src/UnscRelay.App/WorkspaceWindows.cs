using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using UnscRelay.Core;
using Forms=System.Windows.Forms;
namespace UnscRelay.App;
internal static class WorkspaceWindowPosition
{
    [StructLayout(LayoutKind.Sequential)]struct NativeRect{public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out NativeRect rect);
    [DllImport("user32.dll")]static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")]static extern IntPtr GetTopWindow(IntPtr parent);
    [DllImport("user32.dll")]static extern IntPtr GetWindow(IntPtr window,uint command);
    sealed class PhysicalCoordinates:IDisposable{readonly IntPtr prior=SetThreadDpiAwarenessContext(new IntPtr(-4));public void Dispose(){if(prior!=IntPtr.Zero)SetThreadDpiAwarenessContext(prior);}}
    public static WindowArea? Read(DetachedWindow window)
    {
        using var scope=new PhysicalCoordinates();var handle=new WindowInteropHelper(window).Handle;if(handle==IntPtr.Zero||!GetWindowRect(handle,out var rect))return null;return new(rect.Left,rect.Top,rect.Right-rect.Left,rect.Bottom-rect.Top);
    }
    public static WorkspaceWindow Capture(DetachedWindow window,int order)
    {
        var bounds=window.WindowState==WindowState.Normal?Read(window):window.LastNormalArea;bounds??=Read(window);if(bounds==null)throw new IOException("Impossibile leggere la posizione della finestra.");
        using var scope=new PhysicalCoordinates();var screen=Forms.Screen.FromRectangle(new System.Drawing.Rectangle(bounds.Left,bounds.Top,bounds.Width,bounds.Height));var work=screen.WorkingArea;
        return WorkspaceGeometry.Capture(new(screen.DeviceName,work.Left,work.Top,work.Width,work.Height,screen.Primary),bounds,window.LastNonMinimizedState==WindowState.Maximized,order);
    }
    public static bool Apply(DetachedWindow window,WorkspaceWindow saved)
    {
        MonitorArea[] monitors;using(var scope=new PhysicalCoordinates())monitors=Forms.Screen.AllScreens.Select(s=>new MonitorArea(s.DeviceName,s.WorkingArea.Left,s.WorkingArea.Top,s.WorkingArea.Width,s.WorkingArea.Height,s.Primary)).ToArray();
        var plan=WorkspaceGeometry.Resolve(saved,monitors);window.WindowState=WindowState.Normal;
        void Move(WindowArea rect){using var scope=new PhysicalCoordinates();if(!SetWindowPos(new WindowInteropHelper(window).Handle,IntPtr.Zero,rect.Left,rect.Top,rect.Width,rect.Height,0x14))throw new IOException("Impossibile ripristinare la posizione della finestra.");}
        Move(plan.Bounds);var dpi=VisualTreeHelper.GetDpi(window);window.MinWidth=Math.Min(850,plan.Monitor.Width/dpi.DpiScaleX);window.MinHeight=Math.Min(580,plan.Monitor.Height/dpi.DpiScaleY);
        plan=WorkspaceGeometry.Resolve(saved,monitors,(int)Math.Round(window.MinWidth*dpi.DpiScaleX),(int)Math.Round(window.MinHeight*dpi.DpiScaleY));Move(plan.Bounds);window.LastNormalArea=plan.Bounds;
        if(saved.Maximized)window.WindowState=WindowState.Maximized;return plan.Relocated;
    }
    public static Dictionary<string,int> Order(IReadOnlyDictionary<string,DetachedWindow> windows)
    {
        var handles=windows.ToDictionary(p=>new WindowInteropHelper(p.Value).Handle,p=>p.Key);var result=new Dictionary<string,int>();int scanned=0;
        for(var handle=GetTopWindow(IntPtr.Zero);handle!=IntPtr.Zero&&scanned++<10000;handle=GetWindow(handle,2))if(handles.TryGetValue(handle,out var id))result[id]=result.Count;
        foreach(var id in windows.Keys)if(!result.ContainsKey(id))result[id]=result.Count;return result;
    }
}
public partial class MainWindow
{
    readonly Dictionary<string,(WorkspaceWindow Placement,DateTime Expires)> pendingWindowPlacements=[];
    object? WorkspaceWindowAction(string action,JsonElement payload)
    {
        if(action=="workspaceWindowCapture")
        {
            var ids=payload.GetProperty("sessionIds").Deserialize<string[]>()??[];if(ids.Length>30||ids.Distinct().Count()!=ids.Length||ids.Any(id=>!Guid.TryParse(id,out _)))throw new ArgumentException("Schede workspace non valide.");
            var order=WorkspaceWindowPosition.Order(detachedWindows);return ids.Select(id=>detachedWindows.TryGetValue(id,out var window)?WorkspaceWindowPosition.Capture(window,order[id]):null).ToArray();
        }
        if(action=="workspaceWindowPrepare")
        {
            foreach(var key in pendingWindowPlacements.Where(p=>p.Value.Expires<DateTime.UtcNow).Select(p=>p.Key).ToArray())pendingWindowPlacements.Remove(key);
            var id=S(payload,"id");if(!Guid.TryParse(id,out _)||pendingWindowPlacements.Count>=10)throw new ArgumentException("Finestra workspace non valida o limite raggiunto.");var placement=payload.GetProperty("placement").Deserialize<WorkspaceWindow>(ProfileStore.Json)??throw new ArgumentException("Posizione mancante.");placement.Validate();
            if(detachedWindows.TryGetValue(id,out var existing))return new{applied=true,relocated=WorkspaceWindowPosition.Apply(existing,placement)};
            pendingWindowPlacements[id]=(placement,DateTime.UtcNow.AddSeconds(30));return new{applied=false,relocated=false};
        }
        throw new ArgumentException("Comando posizione workspace non valido.");
    }
}
