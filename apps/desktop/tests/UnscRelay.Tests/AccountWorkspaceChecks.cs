using System.Security.Cryptography;
using UnscRelay.Core;
internal static class AccountWorkspaceChecks
{
 public static int Run()
 {
  var root=Directory.CreateTempSubdirectory("relay-account-workspaces-");int count=0;
  void Check(bool ok,string text){if(!ok)throw new Exception(text);count++;Console.WriteLine("PASS workspace: "+text);}
  try
  {
   var spaces=new AccountWorkspaces(root.FullName);Check(spaces.ActiveDirectory()==root.FullName,"an existing installation retains its original data directory");
   spaces.Remember(new(){Owner=new string('A',64),Name="First",Email="one@example.invalid"});var id=spaces.Add("two@example.invalid");Check(id!=Guid.Empty&&spaces.Add("two@example.invalid")==id,"remembered email reuses its dedicated workspace");
   var file=Path.Combine(root.FullName,"keys.vault");File.WriteAllText(file,"synthetic encrypted sentinel");spaces.Select(id);Check(spaces.ActiveDirectory()!=root.FullName&&File.ReadAllText(file)=="synthetic encrypted sentinel","switching never moves or overwrites the original encrypted archive");
   Check(new AccountSyncStore(spaces.ActiveDirectory()).Load().Email=="two@example.invalid","a new workspace prefills only its own remembered email");
   spaces.Remember(new(){Owner=new string('B',64),Name="Second",Email="two@example.invalid"});
   try{spaces.Remember(new(){Owner=new string('A',64),Name="Wrong"});throw new Exception("wrong binding accepted");}catch(InvalidDataException){Check(true,"an existing workspace cannot be rebound to another account");}
   spaces.Select(Guid.Empty);Check(spaces.Load().Accounts.Count==2,"both account identities remain remembered after switching");var secondVault=Path.Combine(spaces.DirectoryFor(id),"keys.vault");File.WriteAllText(secondVault,"second encrypted sentinel");var backup=UpdateBackup.Create(root.FullName,"synthetic");Check(File.ReadAllText(Path.Combine(backup,"Accounts",id.ToString("N"),"keys.vault"))=="second encrypted sentinel","update backup preserves the encrypted vault of every indexed account");spaces.Forget(id);Check(File.ReadAllText(secondVault)=="second encrypted sentinel","forgetting metadata never deletes encrypted account data");
   Check(!File.ReadAllText(Path.Combine(root.FullName,"accounts.dpapi")).Contains("one@example.invalid"),"remembered account metadata is protected at rest");
   try{spaces.Select(Guid.NewGuid());throw new Exception("unknown workspace accepted");}catch(ArgumentException){Check(true,"unlisted workspace IDs are rejected");}
   try{spaces.Add("../bad");throw new Exception("bad email accepted");}catch(ArgumentException){Check(true,"an email can never become a data directory path");}
   Console.WriteLine("Account workspace checks: "+count);return 0;
  }
  finally{root.Delete(true);}
 }
}
