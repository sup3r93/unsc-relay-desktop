using System.IO;
using System.Text.Json;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
    async Task CheckConversionBridge()
    {
        var checks=new Dictionary<string,bool>();string? error=null;
        JsonElement P(object o)=>JsonSerializer.SerializeToElement(o,ProfileStore.Json);
        try
        {
            var fixture=Environment.GetEnvironmentVariable("RELAY_CONVERSION_FIXTURE_ROOT")??throw new IOException("Fixture missing");
            checks["packagedOfficialToolVerified"]=File.Exists(PuttygenPath);
            keyConversion=new KeyConversion(Path.Combine(fixture,"original.key"),"ppk","");conversionId=Guid.NewGuid().ToString("N");
            var status=JsonSerializer.SerializeToElement(await KeyConversionAction("conversionStatus",P(new{})),ProfileStore.Json);
            checks["statusContainsOnlyPublicMetadata"]=status.GetProperty("active").GetBoolean()&&status.GetProperty("source").GetProperty("fingerprint").GetString()==keyConversion.Original.Fingerprint&&!status.ToString().Contains(fixture)&&!status.ToString().Contains("PRIVATE KEY");
            try{await Execute("installUpdate",P(new{}));}catch(InvalidOperationException ex){checks["activeConversionBlocksUpdateBeforeDownload"]=ex.Message.Contains("conversione");}
            await KeyConversionAction("conversionCancel",P(new{}));checks["cancelReleasesSourceAndState"]=!KeyConversionBusy;
            using var unlocked=new FileStream(Path.Combine(fixture,"original.key"),FileMode.Open,FileAccess.Write);checks["sourceUnlockedAfterCancel"]=unlocked.CanWrite;
        }
        catch(Exception ex){error=ex.ToString();}
        finally{CancelKeyConversion();}
        var success=error==null&&checks.Count==5&&checks.Values.All(v=>v);File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"conversion-smoke-result.json"),JsonSerializer.Serialize(new{success,checks,error},ProfileStore.Json));CompleteSmoke(success);
    }
}
