using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
namespace UnscRelay.Core;
// Windows ConPTY: separate input/output workers keep synchronous pipes off the UI thread.
public sealed class LocalTerminal:IAsyncDisposable
{
    readonly object gate=new();readonly BlockingCollection<byte[]> input=new(128);IntPtr console,job;FileStream? outputStream,inputStream;Process? process;Task? readTask,writeTask,closeTask;LocalLaunch? launch;int stopped,started;long queuedBytes;
    public event Action<byte[]>? Output;public event Action<int>? Exited;public bool Running=>Volatile.Read(ref stopped)==0&&process!=null&&!process.HasExited;public int ProcessId=>process?.Id??0;
    LocalLaunch? suppliedLaunch;
    public Task StartExecutable(LocalLaunch command,string scratch,int columns,int rows) { suppliedLaunch=command;return Start(new ConnectionProfile { Protocol="local" },scratch,columns,rows); }
    public async Task Start(ConnectionProfile profile,string scratch,int columns,int rows)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Le sessioni locali richiedono Windows.");if(Interlocked.Exchange(ref started,1)!=0)throw new InvalidOperationException("Sessione gia avviata.");
        await Task.Run(()=>StartNative(profile,scratch,columns,rows));_ = Monitor();
    }
    void StartNative(ConnectionProfile profile,string scratch,int columns,int rows)
    {
        IntPtr inRead=IntPtr.Zero,inWrite=IntPtr.Zero,outRead=IntPtr.Zero,outWrite=IntPtr.Zero,attributes=IntPtr.Zero,environmentBlock=IntPtr.Zero;ProcessInfo child=default;bool initialized=false;
        try{
            launch=suppliedLaunch??LocalLaunch.Create(profile,scratch);Check(CreatePipe(out inRead,out inWrite,IntPtr.Zero,0));Check(CreatePipe(out outRead,out outWrite,IntPtr.Zero,0));HResult(CreatePseudoConsole(Size(columns,rows),inRead,outWrite,0,out console));
            outputStream=new FileStream(new SafeFileHandle(outRead,true),FileAccess.Read,16384,false);outRead=IntPtr.Zero;inputStream=new FileStream(new SafeFileHandle(inWrite,true),FileAccess.Write,4096,false);inWrite=IntPtr.Zero;readTask=Task.Run(ReadOutput);writeTask=Task.Run(WriteInput);
            nuint size=0;InitializeProcThreadAttributeList(IntPtr.Zero,1,0,ref size);attributes=Marshal.AllocHGlobal(checked((int)size));Check(InitializeProcThreadAttributeList(attributes,1,0,ref size));initialized=true;Check(UpdateProcThreadAttribute(attributes,0,(IntPtr)0x20016,console,(nuint)IntPtr.Size,IntPtr.Zero,IntPtr.Zero));
            // Explicit null standard handles prevent inheriting the launcher's redirected pipes instead of ConPTY.
            var startup=new StartupEx{Info=new StartupInfo{Size=Marshal.SizeOf<StartupEx>(),Flags=0x100,StdInput=IntPtr.Zero,StdOutput=IntPtr.Zero,StdError=IntPtr.Zero},Attributes=attributes};if(profile.LocalShell!="wsl"){job=CreateJobObject(IntPtr.Zero,null);Check(job!=IntPtr.Zero);var limits=new JobLimits{Basic=new BasicJobLimits{LimitFlags=0x2000}};Check(SetInformationJobObject(job,9,ref limits,(uint)Marshal.SizeOf<JobLimits>()));}
            // WSL is a service-managed distribution, not an owned Windows process tree. Closing ConPTY closes its client; never shut down WSL globally.
            if(launch.EnvironmentVariables.Count>0) { var environment=System.Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>().ToDictionary(e=>(string)e.Key,e=>(string)e.Value!,StringComparer.OrdinalIgnoreCase);foreach(var pair in launch.EnvironmentVariables)environment[pair.Key]=pair.Value;environmentBlock=Marshal.StringToHGlobalUni(string.Join("\0",environment.OrderBy(e=>e.Key,StringComparer.OrdinalIgnoreCase).Select(e=>e.Key+"="+e.Value))+"\0\0"); }
            Check(CreateProcess(launch.Executable,new StringBuilder(launch.CommandLine),IntPtr.Zero,IntPtr.Zero,false,(profile.LocalShell=="wsl"?0x80000u:0x80004u)|(environmentBlock!=IntPtr.Zero?0x400u:0u),environmentBlock,launch.WorkingDirectory,ref startup,out child));
            if(profile.LocalShell!="wsl")Check(AssignProcessToJobObject(job,child.Process));process=Process.GetProcessById((int)child.ProcessId);if(profile.LocalShell!="wsl"&&ResumeThread(child.Thread)==uint.MaxValue)throw new Win32Exception();
        }catch{if(child.Process!=IntPtr.Zero)TerminateProcess(child.Process,1);Cleanup();process?.Dispose();throw;}
        finally{if(environmentBlock!=IntPtr.Zero)Marshal.FreeHGlobal(environmentBlock);if(initialized)DeleteProcThreadAttributeList(attributes);if(attributes!=IntPtr.Zero)Marshal.FreeHGlobal(attributes);foreach(var h in new[]{inRead,inWrite,outRead,outWrite,child.Process,child.Thread})if(h!=IntPtr.Zero)CloseHandle(h);}
    }
    async Task Monitor(){int code=-1;try{await process!.WaitForExitAsync();code=process.ExitCode;}catch(InvalidOperationException){}finally{await DisposeAsync();process?.Dispose();Exited?.Invoke(code);}}
    void ReadOutput(){try{var buffer=new byte[16384];int count;while((count=outputStream!.Read(buffer))>0)Output?.Invoke(buffer[..count]);}catch(IOException){}catch(ObjectDisposedException){}finally{outputStream?.Dispose();}}
    void WriteInput(){try{foreach(var bytes in input.GetConsumingEnumerable()){try{inputStream!.Write(bytes);inputStream.Flush();}finally{Interlocked.Add(ref queuedBytes,-bytes.Length);}}}catch(IOException){}catch(ObjectDisposedException){}finally{inputStream?.Dispose();}}
    public void Write(string text){if(!Running)throw new IOException("La sessione locale e terminata.");var bytes=Encoding.UTF8.GetBytes(text);if(bytes.Length>1024*1024)throw new IOException("Input troppo grande (massimo 1 MiB).");if(Interlocked.Add(ref queuedBytes,bytes.Length)>1024*1024){Interlocked.Add(ref queuedBytes,-bytes.Length);throw new IOException("Il terminale sta elaborando altro input. Attendi un istante.");}try{if(!input.TryAdd(bytes)){Interlocked.Add(ref queuedBytes,-bytes.Length);throw new IOException("Input del terminale occupato.");}}catch(InvalidOperationException){Interlocked.Add(ref queuedBytes,-bytes.Length);throw new IOException("La sessione locale e terminata.");}}
    public Task Resize(int columns,int rows)=>Task.Run(()=>{lock(gate){if(console!=IntPtr.Zero&&Volatile.Read(ref stopped)==0)HResult(ResizePseudoConsole(console,Size(columns,rows)));}});
    static Coord Size(int cols,int rows)=>new((short)Math.Clamp(cols,2,500),(short)Math.Clamp(rows,1,300));
    public ValueTask DisposeAsync(){lock(gate){if(closeTask==null){Interlocked.Exchange(ref stopped,1);closeTask=Task.Run(Cleanup);}return new ValueTask(closeTask);}}
    void Cleanup(){input.CompleteAdding();if(job!=IntPtr.Zero){CloseHandle(job);job=IntPtr.Zero;}IntPtr pc;lock(gate){pc=console;console=IntPtr.Zero;}if(pc!=IntPtr.Zero)ClosePseudoConsole(pc);try{inputStream?.Dispose();}catch(IOException){}launch?.Dispose();}
    static void Check(bool ok){if(!ok)throw new Win32Exception(Marshal.GetLastWin32Error());}static void HResult(int code){if(code<0)Marshal.ThrowExceptionForHR(code);}
    [StructLayout(LayoutKind.Sequential)]readonly struct Coord(short x,short y){public readonly short X=x,Y=y;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct StartupInfo{public int Size;public string? Reserved,Desktop,Title;public int X,Y,XSize,YSize,XCountChars,YCountChars,FillAttribute,Flags;public short ShowWindow,Reserved2;public IntPtr ReservedPointer,StdInput,StdOutput,StdError;}
    [StructLayout(LayoutKind.Sequential)]struct StartupEx{public StartupInfo Info;public IntPtr Attributes;}
    [StructLayout(LayoutKind.Sequential)]struct ProcessInfo{public IntPtr Process,Thread;public uint ProcessId,ThreadId;}
    [StructLayout(LayoutKind.Sequential)]struct BasicJobLimits{public long PerProcessUserTimeLimit,PerJobUserTimeLimit;public uint LimitFlags;public nuint MinimumWorkingSetSize,MaximumWorkingSetSize;public uint ActiveProcessLimit;public nuint Affinity;public uint PriorityClass,SchedulingClass;}
    [StructLayout(LayoutKind.Sequential)]struct IoCounters{public ulong ReadOperationCount,WriteOperationCount,OtherOperationCount,ReadTransferCount,WriteTransferCount,OtherTransferCount;}
    [StructLayout(LayoutKind.Sequential)]struct JobLimits{public BasicJobLimits Basic;public IoCounters Io;public nuint ProcessMemoryLimit,JobMemoryLimit,PeakProcessMemoryUsed,PeakJobMemoryUsed;}
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool CreatePipe(out IntPtr read,out IntPtr write,IntPtr attributes,uint size);
    [DllImport("kernel32.dll")]static extern int CreatePseudoConsole(Coord size,IntPtr input,IntPtr output,uint flags,out IntPtr console);
    [DllImport("kernel32.dll")]static extern int ResizePseudoConsole(IntPtr console,Coord size);
    [DllImport("kernel32.dll")]static extern void ClosePseudoConsole(IntPtr console);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool InitializeProcThreadAttributeList(IntPtr attributes,int count,uint flags,ref nuint size);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool UpdateProcThreadAttribute(IntPtr attributes,uint flags,IntPtr attribute,IntPtr value,nuint size,IntPtr previous,IntPtr returned);
    [DllImport("kernel32.dll")]static extern void DeleteProcThreadAttributeList(IntPtr attributes);
    [DllImport("kernel32.dll",EntryPoint="CreateProcessW",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateProcess(string app,StringBuilder command,IntPtr processSecurity,IntPtr threadSecurity,bool inherit,uint flags,IntPtr environment,string directory,ref StartupEx startup,out ProcessInfo child);
    [DllImport("kernel32.dll",EntryPoint="CreateJobObjectW",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateJobObject(IntPtr security,string? name);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetInformationJobObject(IntPtr job,int type,ref JobLimits limits,uint length);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
    [DllImport("kernel32.dll",SetLastError=true)]static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32.dll")]static extern bool TerminateProcess(IntPtr process,uint code);
    [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
}
