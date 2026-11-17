using UnscRelay.Core;
static class NativeRecoveryChecks
{
 public static async Task<int> Run()
 {
  int count=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
  var profile=new ConnectionProfile{Protocol="native",Authentication="password",PasswordId="fixture",Native=new(){Kind="ssh",Target="fixture"},Hooks=[new("before-connect","local",@"C:\fixture.exe",[],true),new("after-connect","remote","true",[],true)]};
  var snapshot=NativeRecovery.Snapshot(profile);NativeRecovery.Validate(profile,snapshot,true);Check(true,"Stored credential permits native recovery");
  void Reject(ConnectionProfile p,bool unlocked,string name){try{NativeRecovery.Validate(p,NativeRecovery.Snapshot(p),unlocked);throw new Exception(name);}catch(IOException){Check(true,name);}}
  Reject(profile,false,"Locked vault blocks replacement");Reject(profile with{PasswordId=""},true,"Missing password never prompts during recovery");Reject(profile with{Authentication="auto"},true,"External agent recovery requires manual reopen");Reject(profile with{Native=profile.Native with{ConfigFile=@"C:\fixture.config"}},true,"External config cannot replay arbitrary startup commands");Reject(profile with{Native=profile.Native with{SharedHostKeys=false}},true,"Unmanaged host trust requires manual reopen");Reject(profile with{Native=profile.Native with{IdentityFile=@"C:\key.pub"}},true,"External identity requires manual reopen");
  try{NativeRecovery.Validate(profile with{Name="changed"},snapshot,true);throw new Exception("Changed snapshot accepted");}catch(IOException){Check(true,"Any saved profile change blocks recovery");}
  var key=profile with{Authentication="key",PasswordId="",KeyId="fixture"};NativeRecovery.Validate(key,NativeRecovery.Snapshot(key),true);Check(true,"Stored key permits recovery");
  var calls=new List<string>();var hooks=new NativeHookSession(profile,_=>{},h=>{calls.Add(h.Phase);return Task.CompletedTask;},recovering:true);await hooks.Start();await hooks.Authenticated();await hooks.BeforeClose(true);await hooks.Finished(true);Check(calls.Count==0,"Recovery never replays lifecycle hooks");
  Console.WriteLine("Native recovery checks: "+count);return 0;
 }
}
