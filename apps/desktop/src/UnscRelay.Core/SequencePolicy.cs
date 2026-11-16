namespace UnscRelay.Core;
public static class SequencePolicy
{
    public const double StartupVolume = .8;
    public const double ShutdownVolume = 1;
    public const int DurationSeconds = 5;
    public static UserSettings Apply(UserSettings settings) => settings with { StartupVideo=true, ShutdownVideo=true, StartupVolume=NormalizeVolume(settings.StartupVolume,StartupVolume), ShutdownVolume=NormalizeVolume(settings.ShutdownVolume,ShutdownVolume), AppMusicVolume=NormalizeVolume(settings.AppMusicVolume,.35) };
    public static double NormalizeVolume(double value,double fallback) => double.IsFinite(value)?Math.Clamp(value,0,1):fallback;
    public static bool CanFinish(TimeSpan elapsed,bool mediaFailed) => mediaFailed || elapsed.TotalSeconds >= DurationSeconds-.2;
}
