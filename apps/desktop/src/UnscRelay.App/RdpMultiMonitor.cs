using System.Runtime.InteropServices;

namespace UnscRelay.App;

// Slots follow the Windows MsTscAx type library, IID_IMsRdpClientNonScriptable5.
// Keep the gaps: this is an IUnknown interface, not the default dispatch interface.
[ComImport, Guid("4F6996D5-D7B1-412C-B0FF-063718566907"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IRdpMonitorSettings
{
    void _VtblGap1_32();
    void SetConnectionBarText([MarshalAs(UnmanagedType.BStr)] string text);
    void _VtblGap2_17();
    void SetUseMultimon([MarshalAs(UnmanagedType.VariantBool)] bool enabled);
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetUseMultimon();
}
