namespace UnscRelay.Core;
public static class DesktopAccountNavigation
{
    public static string? Action(string value)=>value switch{
        "profile"=>"UPDATE_PROFILE", "password"=>"UPDATE_PASSWORD", "otp"=>"CONFIGURE_TOTP",
        "passkey"=>"webauthn-register-passwordless", "security-key"=>"webauthn-register", "recovery-codes"=>"unsc-recovery-codes", _=>null};
    public static bool IsIdentityUrl(string value)=>Uri.TryCreate(value,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.Host=="unsc.responda.eu"&&uri.IsDefaultPort&&uri.UserInfo.Length==0&&uri.AbsolutePath.StartsWith("/identity/realms/unsc/",StringComparison.Ordinal);
    public static bool IsCallback(string value)=>Uri.TryCreate(value,UriKind.Absolute,out var uri)&&uri.GetLeftPart(UriPartial.Path)==CloudAccountClient.Callback&&uri.UserInfo.Length==0&&uri.Fragment.Length==0;
}
