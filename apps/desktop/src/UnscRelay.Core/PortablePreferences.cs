namespace UnscRelay.Core;
public sealed record PortableMusic(bool Enabled,double Volume);
public sealed record PortablePreferences(string? Language=null,PortableMusic? Music=null)
{
 public void Validate(){if(Language!=null)InterfaceLocale.Require(Language);if(Music is{} music&&(!double.IsFinite(music.Volume)||music.Volume is <0 or >1))throw new InvalidDataException("Preferenze sincronizzate non valide.");}
 public static PortablePreferences Capture(UserSettings local,PortablePreferences remembered){remembered.Validate();return new(local.SyncLanguage?InterfaceLocale.Require(local.Language):remembered.Language,local.SyncMusic?new(local.AppMusicEnabled,SequencePolicy.NormalizeVolume(local.AppMusicVolume,.35)):remembered.Music);}
 public UserSettings Apply(UserSettings local){Validate();return local with{Language=local.SyncLanguage&&Language!=null?Language:local.Language,AppMusicEnabled=local.SyncMusic&&Music!=null?Music.Enabled:local.AppMusicEnabled,AppMusicVolume=local.SyncMusic&&Music!=null?Music.Volume:local.AppMusicVolume};}
}
