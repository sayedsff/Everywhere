using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Everywhere.Windows.Interop;

/// <summary>
/// Callback for <see cref="LowLevelMouseHook"/>. Return true to block the message.
/// </summary>
internal delegate bool LowLevelMouseHookHandler(nuint wParam, ref MSLLHOOKSTRUCT lParam);

internal class LowLevelMouseHook : IDisposable
{
    public LowLevelMouseHookHandler? Callback;

    private readonly UnhookWindowsHookExSafeHandle mouseHookHandle;
    private GCHandle mouseHookProcHandle;

    public LowLevelMouseHook(LowLevelMouseHookHandler? callback = null)
    {
        Callback = callback;

        using var hModule = PInvoke.GetModuleHandle(null);
        var mouseHookProc = new HOOKPROC(MouseHookProc);
        mouseHookProcHandle = GCHandle.Alloc(mouseHookProc);
        mouseHookHandle = PInvoke.SetWindowsHookEx(
            WINDOWS_HOOK_ID.WH_MOUSE_LL,
            mouseHookProc,
            hModule,
            0);
    }

    ~LowLevelMouseHook()
    {
        Dispose();
    }

    private unsafe LRESULT MouseHookProc(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code < 0) return PInvoke.CallNextHookEx(null, code, wParam, lParam);

        ref var hookStruct = ref Unsafe.AsRef<MSLLHOOKSTRUCT>(lParam.Value.ToPointer());
        var handled = Callback?.Invoke(wParam, ref hookStruct) ?? false;
        return handled ? (LRESULT)1 : PInvoke.CallNextHookEx(null, code, wParam, lParam);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        mouseHookHandle.Dispose();
        mouseHookProcHandle.Free();
    }
}