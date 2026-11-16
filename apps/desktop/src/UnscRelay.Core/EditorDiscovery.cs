using Microsoft.Win32;
using System.Runtime.Versioning;
namespace UnscRelay.Core;
public static class EditorDiscovery
{
    public static string SelectNotepadPlusPlus(IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase)) {
            try {
                var path=candidate.Trim().Trim('"');
                if (Path.IsPathFullyQualified(path) && string.Equals(Path.GetFileName(path),"notepad++.exe",StringComparison.OrdinalIgnoreCase) && File.Exists(path)) return Path.GetFullPath(path);
            } catch (ArgumentException) {} catch (IOException) {} catch (UnauthorizedAccessException) {}
        }
        return "";
    }
    [SupportedOSPlatform("windows")]
    public static string FindNotepadPlusPlus()
    {
        var candidates=new List<string>();
        foreach(var hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})
        foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32}) {
            try {
                using var root=RegistryKey.OpenBaseKey(hive,view);
                using var app=root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\notepad++.exe");
                if(app?.GetValue("") is string executable)candidates.Add(executable);
                using var uninstall=root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Notepad++");
                if(uninstall?.GetValue("InstallLocation") is string location)candidates.Add(Path.Combine(location,"notepad++.exe"));
            }catch(System.Security.SecurityException){}catch(UnauthorizedAccessException){}catch(IOException){}catch(ArgumentException){}
        }
        foreach(var folder in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs")})
            if(folder.Length>0)candidates.Add(Path.Combine(folder,"Notepad++","notepad++.exe"));
        foreach(var folder in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator,StringSplitOptions.RemoveEmptyEntries))
            try{candidates.Add(Path.Combine(folder.Trim().Trim('"'),"notepad++.exe"));}catch(ArgumentException){}
        return SelectNotepadPlusPlus(candidates);
    }
    public static UserSettings ApplyAutomatic(UserSettings settings,string detected)
    {
        var mode=settings.EditorPreference;
        if(mode.Length==0)mode=settings.ExternalEditor.Length>0?"custom":"auto";
        return mode=="auto"?settings with{ExternalEditor=detected,EditorPreference="auto"}:settings with{EditorPreference=mode};
    }
}
