using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Everywhere.Windows.Interop;

[GeneratedComInterface]
[Guid("22118adf-23f1-4801-bcfa-66cbf48cc51b")]
internal partial interface IInteropCompositorFactoryPartner
{
    // Placeholder for IInspectable methods
    void Stub3();
    void Stub4();
    void Stub5();

    nint CreateInteropCompositor(
        nint renderingDevice,
        nint callback,
        in Guid iid);

    void CheckEnabled(
        [MarshalAs(UnmanagedType.Bool)] out bool enableInteropCompositor,
        [MarshalAs(UnmanagedType.Bool)] out bool enableExposeVisual);
}
