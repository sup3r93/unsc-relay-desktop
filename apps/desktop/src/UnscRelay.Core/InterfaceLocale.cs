namespace UnscRelay.Core;
public static class InterfaceLocale
{
 public static bool Supported(string? value)=>value is "it" or "en" or "fr" or "de" or "es";
 public static string Normalize(string? value)=>Supported(value)?value!:"it";
 public static string Require(string? value)=>Supported(value)?value!:throw new ArgumentException("Lingua non supportata.");
}
