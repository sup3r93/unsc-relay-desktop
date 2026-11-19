using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UnscRelay.Core;
namespace UnscRelay.App;
internal sealed class ExportDragWindow : Window
{
    public CancellationTokenSource Cancellation {get;}=new();
    readonly TextBlock status=new(){Text="Preparazione…",TextWrapping=TextWrapping.Wrap,Margin=new(0,14,0,14)};
    readonly Border pad=new(){Background=new SolidColorBrush(Color.FromRgb(29,49,39)),BorderBrush=new SolidColorBrush(Color.FromRgb(166,188,119)),BorderThickness=new(1),Padding=new(22),MinHeight=100};
    string[] paths=[];Point press;bool armed;
    public bool Busy {get;private set;}=true;
    public ExportDragWindow()
    {
        Title="UNSC Relay · trascina verso Esplora file";Width=620;Height=410;MinWidth=500;MinHeight=360;Background=new SolidColorBrush(Color.FromRgb(9,20,23));Foreground=new SolidColorBrush(Color.FromRgb(221,232,211));WindowStartupLocation=WindowStartupLocation.CenterScreen;
        var stack=new StackPanel{Margin=new(24)};stack.Children.Add(new TextBlock{Text="SFTP / ESPORTAZIONE LOCALE",FontFamily=new FontFamily("Consolas"),Foreground=new SolidColorBrush(Color.FromRgb(173,194,128))});stack.Children.Add(new TextBlock{Text="Trascina in Esplora file",FontSize=24,Margin=new(0,12,0,0)});stack.Children.Add(status);pad.Child=new TextBlock{Text="Attendi il download completo…",TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};stack.Children.Add(pad);stack.Children.Add(new TextBlock{Text="Le copie temporanee sono in chiaro sul PC. Nessun file remoto viene spostato o eliminato. Windows gestisce la copia e gli eventuali conflitti nella cartella di destinazione.",TextWrapping=TextWrapping.Wrap,FontSize=12,Margin=new(0,14,0,0)});Content=stack;
        pad.MouseLeftButtonDown+=(_,e)=>{press=e.GetPosition(pad);armed=true;};pad.MouseLeftButtonUp+=(_,_)=>armed=false;
        pad.MouseMove+=(_,e)=>{if(!armed||Busy||paths.Length==0||e.LeftButton!=MouseButtonState.Pressed)return;var position=e.GetPosition(pad);if(Math.Abs(position.X-press.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(position.Y-press.Y)<SystemParameters.MinimumVerticalDragDistance)return;armed=false;var data=CreateFileDrop(paths);var result=DragDrop.DoDragDrop(pad,data,DragDropEffects.Copy);status.Text=result==DragDropEffects.Copy?"Copia consegnata a Windows. Puoi trascinare di nuovo gli stessi elementi.":"Trascinamento annullato. Le copie sono ancora disponibili.";};
        Closing+=(_,_)=>{if(Busy)Cancellation.Cancel();};
    }
    internal static DataObject CreateFileDrop(string[] paths){if(paths.Length==0||paths.Any(p=>!File.Exists(p)&&!Directory.Exists(p)))throw new IOException("Copie non disponibili.");return new DataObject(DataFormats.FileDrop,paths);}
    public void Progress(string message){status.Text=message;}
    public void Ready(string[] files){paths=files;Busy=false;status.Text="Download completato · "+files.Length+" elementi pronti. Tieni premuto il riquadro e trascinalo nella cartella di Esplora file.";((TextBlock)pad.Child).Text="↗ TRASCINA QUI → ESPLORA FILE\n"+string.Join(" · ",files.Select(Path.GetFileName));}
    public void Failed(string message){Busy=false;status.Text=message;((TextBlock)pad.Child).Text="Nessun file pronto. Chiudi questa finestra e riprova.";}
}
public partial class MainWindow
{
    readonly List<ExportDragWindow> exportWindows=[];
    int exportOperations;
    bool ExportBusy=>exportOperations>0||PlainExportBusy||AccountErasureBusy;
    void CloseExportWindows(){foreach(var window in exportWindows.ToArray())window.Close();}
    async Task<object?> PrepareExplorerExport(JsonElement payload)
    {
        if(ExportBusy)throw new IOException("Attendi o annulla la preparazione precedente.");
        if(exportWindows.Count>=5)throw new IOException("Chiudi alcune finestre di esportazione prima di prepararne altre.");
        var session=Session(payload);if(!session.FilesAvailable)throw new IOException("SFTP non disponibile.");
        var cacheRoot=Path.Combine(dataPath,"DragExports");if(ExplorerExport.CacheBytes(cacheRoot)>2L*1024*1024*1024-ExplorerExport.MaxBytes)throw new IOException("La cartella delle copie temporanee è piena. Apri i dati locali e libera DragExports prima di riprovare.");
        var root=Path.Combine(cacheRoot,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var window=new ExportDragWindow();exportWindows.Add(window);window.Closed+=(_,_)=>exportWindows.Remove(window);window.Show();
        var token=window.Cancellation.Token;exportOperations++;
        try
        {
            var request=JsonSerializer.SerializeToElement(new{sessionId=session.Id,direction="download",localDirectory=root,remoteDirectory="/",paths=payload.GetProperty("paths")},ProfileStore.Json);
            var planned=JsonSerializer.SerializeToElement(await PlanFiles(request),ProfileStore.Json);token.ThrowIfCancellationRequested();
            if(planned.GetProperty("skippedLinks").GetInt32()>0)throw new IOException("La selezione contiene collegamenti simbolici: seleziona soltanto file e cartelle reali.");
            var entries=ExplorerExport.Validate(root,planned.GetProperty("items").EnumerateArray().Select(item=>new DragExportEntry(S(item,"local"),S(item,"remote"),item.GetProperty("directory").GetBoolean(),item.GetProperty("source").GetProperty("size").GetInt64())));
            var top=entries.Where(e=>Path.GetDirectoryName(e.Local)==root).Select(e=>e.Local).ToArray();if(top.Length==0)throw new IOException("Selezione non esportabile.");
            long completed=0,total=entries.Where(e=>!e.Directory).Sum(e=>e.Size);
            foreach(var entry in entries)
            {
                token.ThrowIfCancellationRequested();var hash=entry.Directory?null:await session.RemoteHashAsync(entry.Remote,token,entry.Size);
                var requestFile=new TransferRequest(Guid.NewGuid().ToString("N"),entry.Local,entry.Remote,false,"new",CreateParents:true,Directory:entry.Directory,ExpectedSourceHash:hash,ExpectedMissingTarget:true,MaximumSourceBytes:entry.Directory?0:entry.Size);
                await session.TransferAdvancedAsync(requestFile,(done,_)=>Dispatcher.BeginInvoke(()=>window.Progress("Download · "+Path.GetFileName(entry.Local)+" · "+(total==0?0:Math.Min(100,(completed+(long)done)*100/total))+"%")),token);completed+=entry.Directory?0:entry.Size;
            }
            token.ThrowIfCancellationRequested();File.WriteAllText(Path.Combine(root,".relay-export"),DateTime.UtcNow.ToString("O"));window.Ready(top);return new{ready=true,count=top.Length};
        }
        catch(Exception ex){window.Failed(ex is OperationCanceledException?"Preparazione annullata.":ex.Message);if(Path.GetFileName(root).Length==32&&Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.Combine(dataPath,"DragExports"))+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&Directory.Exists(root))Directory.Delete(root,true);if(ex is not OperationCanceledException)throw;return null;}
        finally{exportOperations--;}
    }
}
