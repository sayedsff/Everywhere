using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Everywhere.Windows.Interop;

[GeneratedComInterface]
[Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[ComVisible(true)]
internal partial interface IGraphicsCaptureItemInterop
{
    // https://learn.microsoft.com/windows/win32/api/windows.graphics.capture.interop/nf-windows-graphics-capture-interop-igraphicscaptureiteminterop-createforwindow
    nint CreateForWindow(nint window, in Guid iid);

    // https://learn.microsoft.com/windows/win32/api/windows.graphics.capture.interop/nf-windows-graphics-capture-interop-igraphicscaptureiteminterop-createformonitor
    nint CreateForMonitor(nint monitor, in Guid iid);
}