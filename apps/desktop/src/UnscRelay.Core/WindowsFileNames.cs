namespace UnscRelay.Core;
public static class WindowsFileNames
{
 public static bool IsReserved(string name){var stem=name.Split('.')[0].TrimEnd(' ','.');if(stem.Equals("CON",StringComparison.OrdinalIgnoreCase)||stem.Equals("PRN",StringComparison.OrdinalIgnoreCase)||stem.Equals("AUX",StringComparison.OrdinalIgnoreCase)||stem.Equals("NUL",StringComparison.OrdinalIgnoreCase)||stem.Equals("CONIN$",StringComparison.OrdinalIgnoreCase)||stem.Equals("CONOUT$",StringComparison.OrdinalIgnoreCase))return true;return stem.Length==4&&(stem.StartsWith("COM",StringComparison.OrdinalIgnoreCase)||stem.StartsWith("LPT",StringComparison.OrdinalIgnoreCase))&&(stem[3] is >= '1' and <= '9' or '¹' or '²' or '³');}
}
