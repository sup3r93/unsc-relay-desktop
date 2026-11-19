using System.Text.Json;
namespace UnscRelay.Core;
public sealed class NativeInterfaceText
{
 readonly Dictionary<string,Dictionary<string,string>> catalog;
 public NativeInterfaceText(string directory){catalog=JsonSerializer.Deserialize<Dictionary<string,Dictionary<string,string>>>(LocalDataFiles.Read(directory,"interface.json",4*1024*1024))??throw new InvalidDataException("Catalogo dell’interfaccia non valido.");if(catalog.Count!=5||new[]{"it","en","fr","de","es"}.Any(language=>!catalog.TryGetValue(language,out var entries)||entries is null||entries.Count>5000||entries.Keys.Any(key=>key.Length>8192)||entries.Values.Any(value=>value is null||value.Length>8192)))throw new InvalidDataException("Lingue dell’interfaccia non valide.");}
 public string Text(string source,string? language)=>catalog[InterfaceLocale.Normalize(language)].GetValueOrDefault(source,source);
}
public static class LocalLanguageChoice
{
 sealed record Choice(int Version,string Language);
 public static string Read(string directory){const string name="interface-language.json";if(!File.Exists(LocalDataFiles.Child(directory,name)))return "it";try{var value=JsonSerializer.Deserialize<Choice>(LocalDataFiles.Read(directory,name,512));return value is{Version:1}?InterfaceLocale.Normalize(value.Language):"it";}catch(Exception error)when(error is JsonException or InvalidDataException){return "it";}}
 public static void Save(string directory,string language)=>LocalDataFiles.Write(directory,"interface-language.json",JsonSerializer.SerializeToUtf8Bytes(new Choice(1,InterfaceLocale.Require(language))));
}
