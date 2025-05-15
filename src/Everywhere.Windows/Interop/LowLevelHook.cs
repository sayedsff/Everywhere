using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Everywhere.Windows.Interop;

/// <summary>
/// Callback for <see cref="LowLevelHook"/>. Return true to block the message.
/// </summary>
internal delegate bool LowLevelHookHandler<T>(nuint wParam, ref T lParam) where T : unmanaged;

internal abstract class LowLevelHook<T> : IDisposable where T : unmanaged
{
    public event LowLevelHookHandler<T>? Callback;

    private readonly UnhookWindowsHookExSafeHandle hookHandle;
    private GCHandle hookProcHandle;

    public LowLevelHook(WINDOWS_HOOK_ID id, LowLevelHookHandler<T>? callback = null)
    {
        Callback = callback;

        using var hModule = PInvoke.GetModuleHandle(null);
        var hookProc = new HOOKPROC(HookProc);
        hookProcHandle = GCHandle.Alloc(hookProc);
        hookHandle = PInvoke.SetWindowsHookEx(
            id,
            hookProc,
            hModule,
            0);
    }

    ~LowLevelHook()
    {
        Dispose();
    }

    private unsafe LRESULT HookProc(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code < 0) return PInvoke.CallNextHookEx(null, code, wParam, lParam);

        ref var hookStruct = ref Unsafe.AsRef<T>(lParam.Value.ToPointer());
        var handled = Callback?.Invoke(wParam, ref hookStruct) ?? false;
        return handled ? (LRESULT)1 : PInvoke.CallNextHookEx(null, code, wParam, lParam);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        hookHandle.Dispose();
        hookProcHandle.Free();
    }
}

internal class LowLevelMouseHook(LowLevelHookHandler<MSLLHOOKSTRUCT>? callback = null)
    : LowLevelHook<MSLLHOOKSTRUCT>(WINDOWS_HOOK_ID.WH_MOUSE_LL, callback);

internal class LowLevelKeyboardHook(LowLevelHookHandler<KBDLLHOOKSTRUCT>? callback = null)
    : LowLevelHook<KBDLLHOOKSTRUCT>(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, callback);