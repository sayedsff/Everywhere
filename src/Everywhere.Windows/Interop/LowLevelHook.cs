using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Everywhere.Windows.Interop;

/// <summary>
/// Callback for <see cref="LowLevelHook{T}"/>. Return true to block the message.
/// </summary>
internal delegate void LowLevelHookHandler<T>(nuint wParam, ref T lParam, ref bool blockNext) where T : unmanaged;

internal abstract class LowLevelHook<T> : IDisposable where T : unmanaged
{
    public event LowLevelHookHandler<T>? Callback;

    private readonly UnhookWindowsHookExSafeHandle _hookHandle;
    private GCHandle _hookProcHandle;

    protected LowLevelHook(WINDOWS_HOOK_ID id, LowLevelHookHandler<T>? callback = null)
    {
        Callback = callback;

        using var hModule = PInvoke.GetModuleHandle(null);
        var hookProc = new HOOKPROC(HookProc);
        _hookProcHandle = GCHandle.Alloc(hookProc);
        _hookHandle = PInvoke.SetWindowsHookEx(
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
        var blockNext = false;
        Callback?.Invoke(wParam, ref hookStruct, ref blockNext);
        return blockNext ? (LRESULT)1 : PInvoke.CallNextHookEx(null, code, wParam, lParam);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        _hookHandle.Dispose();
        _hookProcHandle.Free();
    }
}

internal class LowLevelMouseHook(LowLevelHookHandler<MSLLHOOKSTRUCT>? callback = null)
    : LowLevelHook<MSLLHOOKSTRUCT>(WINDOWS_HOOK_ID.WH_MOUSE_LL, callback);

internal class LowLevelKeyboardHook(LowLevelHookHandler<KBDLLHOOKSTRUCT>? callback = null)
    : LowLevelHook<KBDLLHOOKSTRUCT>(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, callback);