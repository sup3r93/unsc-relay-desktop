namespace UnscRelay.Core;
public static class ProductionFilePolicy
{
 public static bool IsProduction(ConnectionProfile profile)=>profile.Environment?.Trim().Equals("produzione",StringComparison.OrdinalIgnoreCase)==true||profile.Environment?.Trim().Equals("production",StringComparison.OrdinalIgnoreCase)==true;
 public static bool RequiresConfirmation(ConnectionProfile profile,int count){if(count is <1 or >1000)throw new ArgumentOutOfRangeException(nameof(count));return IsProduction(profile);}
}
