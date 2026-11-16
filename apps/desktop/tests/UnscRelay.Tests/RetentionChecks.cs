using UnscRelay.Core;
internal static class RetentionChecks
{
    public static int Run()
    {
        int count=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
        var root=Path.Combine(Path.GetTempPath(),"relay-retention-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var now=DateTime.UtcNow;
        string FileAt(string name,int days,bool valid=true){var path=Path.Combine(root,name);File.WriteAllBytes(path,System.Text.Encoding.ASCII.GetBytes(valid?"UREC0001fixture":"not recording"));File.SetLastWriteTimeUtc(path,now.AddDays(-days));return path;}
        try
        {
            var old=FileAt(Guid.NewGuid().ToString("N")+".urec",90);var active=FileAt(Guid.NewGuid().ToString("N")+".urec",90);var recent=FileAt(Guid.NewGuid().ToString("N")+".urec",1);var unknown=FileAt("personal.urec",90);var bad=FileAt(Guid.NewGuid().ToString("N")+".urec",90,false);
            Check(RecordingRetention.Apply(root,0,[],now).Deleted==0&&File.Exists(old),"Retention disabled preserves existing recordings");
            var result=RecordingRetention.Apply(root,30,[active],now);Check(result.Deleted==1&&!File.Exists(old),"Expired owned recording deleted after opt-in");Check(File.Exists(active),"Active recorder excluded");Check(File.Exists(recent),"Recent recorder preserved");Check(File.Exists(unknown)&&File.Exists(bad),"Non-owned names and invalid header preserved");Check(RecordingRetention.Apply(root,30,[active],now).Deleted==0,"Repeated cleanup is idempotent");
            var data=new AppData{Settings=new(RecordingRetentionDays:30)};var store=new ProfileStore(root);store.Save(data);Check(store.Load().Settings.RecordingRetentionDays==30,"Retention survives settings persistence");
            Check(SequencePolicy.Apply(data.Settings).RecordingRetentionDays==30,"Sequence policy preserves recording retention");
            bool rejected=false;try{RecordingRetention.Apply(root,-1,[],now);}catch(ArgumentException){rejected=true;}Check(rejected,"Invalid age rejected without deleting files");
            Console.WriteLine($"{count} retention checks passed");return 0;
        }finally{if(Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(root).StartsWith("relay-retention-",StringComparison.Ordinal))Directory.Delete(root,true);}
    }
}
