using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;
using Avalonia.Controls;
using Everywhere.Interfaces;

namespace Everywhere.Windows.Services;

public class Win32PlatformHandleHelper : IPlatformHandleHelper
{
    // ReSharper disable InconsistentNaming
    // ReSharper disable IdentifierTypo
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    // ReSharper restore InconsistentNaming
    // ReSharper restore IdentifierTypo

    public void InitializeFloatingWindow(Window window)
    {
        var thisHWnd = window.TryGetPlatformHandle()?.Handle ?? 0;
        if (thisHWnd == 0)
        {
            throw new InvalidOperationException("Failed to get platform handle for the top-level window.");
        }

        var targetHWnd = PInvoke.GetForegroundWindow();
        if (targetHWnd == 0)
        {
            throw new InvalidOperationException("Failed to get platform handle for the target window.");
        }

        Win32Properties.AddWindowStylesCallback(window, WindowStylesCallback);

        WINEVENTPROC lpWinEventProc = WinEventProc;
        PInvoke.SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND,
            EVENT_SYSTEM_FOREGROUND,
            HMODULE.Null,
            lpWinEventProc,
            0,
            0,
            WINEVENT_OUTOFCONTEXT);
        window.Closed += delegate { GC.KeepAlive(lpWinEventProc); };

        static (uint style, uint exStyle) WindowStylesCallback(uint style, uint exStyle)
        {
            return (style, exStyle |
                (uint)WINDOW_EX_STYLE.WS_EX_NOACTIVATE |
                (uint)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW |
                (uint)WINDOW_EX_STYLE.WS_EX_TOPMOST);
        }

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
                window.Close();
                PInvoke.UnhookWinEvent(hWinEventHook);
            }
        }
    }
}