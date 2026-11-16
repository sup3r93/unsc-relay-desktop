using UnscRelay.Core;
static class ExplorerChecks
{
    public static int Run()
    {
        var root=Path.Combine(Path.GetTempPath(),"relay-explorer-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);int count=0;
        void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
        void Reject(Action action,string name){try{action();}catch(IOException){Check(true,name);return;}throw new Exception("Accepted: "+name);}
        DragExportEntry Entry(string name,long size=1)=>new(Path.Combine(root,name),"/"+name,false,size);
        try
        {
            Check(ExplorerExport.Validate(root,[Entry("valid.txt")]).Length==1,"Valid Windows export path accepted");
            Reject(()=>ExplorerExport.Validate(root,[]),"Empty selection rejected");Reject(()=>ExplorerExport.Validate(root,Enumerable.Range(0,1001).Select(i=>Entry(i+".txt"))),"Element count bound enforced");
            Reject(()=>ExplorerExport.Validate(root,[Entry("../outside.txt")]),"Traversal outside cache rejected");Reject(()=>ExplorerExport.Validate(root,[Entry("file.txt"),Entry("FILE.txt")]),"Case-insensitive Windows collisions rejected");
            foreach(var name in new[]{"CON","nul.txt","COM1.dat","LPT9","trailing.","trailing ","stream:ads"})Reject(()=>ExplorerExport.Validate(root,[Entry(name)]),"Invalid Windows file rejected: "+name);
            Reject(()=>ExplorerExport.Validate(root,[Entry("big",ExplorerExport.MaxBytes+1)]),"Per-export byte bound enforced");Reject(()=>ExplorerExport.Validate(root,[Entry("one",ExplorerExport.MaxBytes),Entry("two",1)]),"Cumulative byte bound enforced");
            Reject(()=>ExplorerExport.Validate(root,[Entry("negative",-1)]),"Negative size rejected");Reject(()=>ExplorerExport.Validate(root,[Entry("parent"),Entry(Path.Combine("parent","child"))]),"File and directory conflict rejected");
            var old=Path.Combine(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(old);File.WriteAllText(Path.Combine(old,".relay-export"),"fixture");File.WriteAllText(Path.Combine(old,"temporary.txt"),"fixture");File.SetLastWriteTimeUtc(Path.Combine(old,".relay-export"),DateTime.UtcNow.AddDays(-8));
            var recent=Path.Combine(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(recent);File.WriteAllText(Path.Combine(recent,".relay-export"),"fixture");var unrelated=Path.Combine(root,"personal");Directory.CreateDirectory(unrelated);File.WriteAllText(Path.Combine(unrelated,"keep.txt"),"preserved");
            Check(ExplorerExport.CacheBytes(root)>0,"Cache usage measured");ExplorerExport.Prune(root,DateTime.UtcNow);Check(!Directory.Exists(old),"Expired identified export cache removed");Check(Directory.Exists(recent)&&File.Exists(Path.Combine(unrelated,"keep.txt")),"Recent exports and unrelated folders preserved");Console.WriteLine(count+" Explorer checks passed");return 0;
        }
        finally{if(Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(root).StartsWith("relay-explorer-test-"))Directory.Delete(root,true);}
    }
}
