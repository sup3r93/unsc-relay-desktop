namespace UnscRelay.Core;
public static class SftpPermissions
{
 public static short Parse(string value){if(value.Length is not(3 or 4)||value.Any(character=>character is <'0' or >'7'))throw new ArgumentException("Permessi ottali non validi.");return Convert.ToInt16(value,System.Globalization.CultureInfo.InvariantCulture);}
 public static void Validate(short value){if(value<0||value>7777||value.ToString(System.Globalization.CultureInfo.InvariantCulture).Any(character=>character>'7'))throw new ArgumentException("Permessi ottali non validi.");}
 public static short FromLegacyBits(short value){if(value is <0 or >4095)throw new ArgumentException("Permessi precedenti non validi.");return Convert.ToInt16(Convert.ToString(value,8),System.Globalization.CultureInfo.InvariantCulture);}
}
