using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
namespace UnscRelay.App;
internal static class LanguageResources
{
    [ModuleInitializer]
    internal static void Initialize()=>AssemblyLoadContext.Default.Resolving+=Resolve;
    static Assembly? Resolve(AssemblyLoadContext context,AssemblyName name)
    {
        var culture=name.CultureName;
        if(string.IsNullOrEmpty(culture)||name.Name==null||!System.Text.RegularExpressions.Regex.IsMatch(name.Name,@"^[A-Za-z0-9_.-]+\.resources$")||!System.Text.RegularExpressions.Regex.IsMatch(culture,@"^[a-zA-Z]{2,8}(-[a-zA-Z0-9]{2,8})*$"))return null;
        var root=new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        var path=System.IO.Path.Combine(root.Name.Equals("Librerie",StringComparison.OrdinalIgnoreCase)?root.Parent!.FullName:root.FullName,"languages",culture,name.Name+".dll");
        return System.IO.File.Exists(path)?context.LoadFromAssemblyPath(path):null;
    }
}
