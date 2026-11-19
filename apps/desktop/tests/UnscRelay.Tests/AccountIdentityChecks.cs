using System.Text.Json;
using UnscRelay.Core;
internal static class AccountIdentityChecks
{
 public static int Run()
 {
  var root=Directory.CreateTempSubdirectory("relay-account-identities-");
  try{
   var legacy=JsonSerializer.Deserialize<RememberedAccount>("{\"Id\":\"00000000-0000-0000-0000-000000000000\",\"Owner\":\"\",\"Email\":\"fixture@example.invalid\",\"Name\":\"Fixture\"}")!;
   if(legacy.Alias!=""||legacy.Color!="olive")throw new Exception("Legacy identity defaults");
   var spaces=new AccountWorkspaces(root.FullName);var first=spaces.Add("first@example.invalid");var second=spaces.Add("second@example.invalid");spaces.Customize(first,"Work","blue");var values=spaces.Load();if(values.Accounts.Single(a=>a.Id==first).Alias!="Work"||values.Accounts.Single(a=>a.Id==second).Alias!="")throw new Exception("Identity isolation");
   foreach(var invalid in new[]{"javascript:alert(1)","#ffffff"}){try{spaces.Customize(first,"Work",invalid);throw new Exception("Invalid color accepted");}catch(ArgumentException){}}
   try{spaces.Customize(first,"Unsafe\nname","blue");throw new Exception("Control accepted");}catch(ArgumentException){}
   spaces.Select(first);spaces.Remember(new(){Owner=new string('A',64),Email="first@example.invalid",Name="Updated real name"});if(spaces.Load().Accounts.Single(a=>a.Id==first).Alias!="Work")throw new Exception("Login overwrote alias");Console.WriteLine("PASS: legacy identity compatibility, account isolation, validation and retained customization");return 0;
  }finally{root.Delete(true);}
 }
}
