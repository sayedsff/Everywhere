using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Everywhere.Interfaces;

namespace Everywhere.Windows.Services;

public class Win32PlatformHelper : IPlatformHelper
{
    // ReSharper disable InconsistentNaming
    // ReSharper disable IdentifierTypo
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;

    private enum WINDOW_MESSAGE
    {
        MA_NOACTIVATE = 0x0003,
        WM_ACTIVATE = 0x0006,
        WM_SETFOCUS = 0x0007,
        WM_KILLFOCUS = 0x0008,
        WM_ACTIVATEAPP = 0x001C,
        WM_MOUSEACTIVATE = 0x0021,
        WM_NCACTIVATE = 0x0086,
    }
    // ReSharper restore InconsistentNaming
    // ReSharper restore IdentifierTypo

    public unsafe void SetWindowNoFocus(Window window)
    {
        Win32Properties.AddWindowStylesCallback(window, WindowStylesCallback);

        static (uint style, uint exStyle) WindowStylesCallback(uint style, uint exStyle)
        {
            return (style, exStyle |
                (uint)WINDOW_EX_STYLE.WS_EX_NOACTIVATE |
                (uint)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW |
                (uint)WINDOW_EX_STYLE.WS_EX_TOPMOST);
        }

        Win32Properties.AddWndProcHookCallback(window, WndProcHookCallback);

        IntPtr WndProcHookCallback(IntPtr hWnd, uint msg, IntPtr wparam, IntPtr lparam, ref bool handled)
        {
            // handle and block all activate messages
            switch (msg)
            {
                case (uint)WINDOW_MESSAGE.WM_MOUSEACTIVATE:
                case (uint)WINDOW_MESSAGE.WM_ACTIVATE:
                case (uint)WINDOW_MESSAGE.WM_SETFOCUS:
                case (uint)WINDOW_MESSAGE.WM_KILLFOCUS:
                case (uint)WINDOW_MESSAGE.WM_ACTIVATEAPP:
                case (uint)WINDOW_MESSAGE.WM_NCACTIVATE:
                    handled = true;
                    return (IntPtr)WINDOW_MESSAGE.MA_NOACTIVATE;
                default:
                    return IntPtr.Zero;
            }
        }

        // TODO: Following is broken
        uint tid = 0, targetTid = 0;

        window.GotFocus += (_, e) =>
        {
            if (e.Source is not TextBox) return;
            tid = PInvoke.GetCurrentThreadId();
            var targetHWnd = PInvoke.GetForegroundWindow();
            targetTid = PInvoke.GetWindowThreadProcessId(targetHWnd, null);
            PInvoke.AttachThreadInput(targetTid, tid, true);
        };

        window.LostFocus += (_, e) =>
        {
            if (e.Source is not TextBox) return;
            PInvoke.AttachThreadInput(targetTid, tid, false);
            tid = targetTid = 0;
        };

        window.PropertyChanged += (_, e) =>
        {
            if (e.Property != Visual.IsVisibleProperty || e.NewValue is not false) return;
#pragma warning disable CS0618 // 类型或成员已过时
            window.FocusManager?.ClearFocus();  // why, avalonia, why!!!!!!!!!!!!!!!!!!
#pragma warning restore CS0618 // 类型或成员已过时
            PInvoke.AttachThreadInput(targetTid, tid, false);
            tid = targetTid = 0;
        };
    }

    public void SetWindowAutoHide(Window window)
    {
        var thisHWnd = window.TryGetPlatformHandle()?.Handle ?? 0;
        if (thisHWnd == 0)
        {
            throw new InvalidOperationException("Failed to get platform handle for the top-level window.");
        }

        var targetHWnd = HWND.Null;
        window.Loaded += delegate
        {
            targetHWnd = PInvoke.GetForegroundWindow();
            if (targetHWnd == 0)
            {
                throw new InvalidOperationException("Failed to get platform handle for the target window.");
            }
        };
        window.Unloaded += delegate
        {
            targetHWnd = HWND.Null;
        };

        var lpWinEventProc = new WINEVENTPROC(WinEventProc);
        var handle = GCHandle.Alloc(lpWinEventProc);
        var winEventHook = PInvoke.SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND,
            EVENT_SYSTEM_FOREGROUND,
            HMODULE.Null,
            lpWinEventProc,
            0,
            0,
            WINEVENT_OUTOFCONTEXT);
        window.Closed += delegate
        {
            PInvoke.UnhookWinEvent(winEventHook);
            handle.Free();
        };

        void WinEventProc(
            HWINEVENTHOOK hWinEventHook,
            uint eventType,
            HWND hWnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime)
        {
            var foregroundWindow = PInvoke.GetForegroundWindow();
            if (foregroundWindow != targetHWnd && foregroundWindow != thisHWnd)
            {
                window.IsVisible = false;
            }
        }
    }

    [DllImport("kernel32.dll")]
    private static extern int GetLastError();
}