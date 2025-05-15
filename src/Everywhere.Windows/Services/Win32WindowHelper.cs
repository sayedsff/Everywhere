using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;
using Avalonia;
using Avalonia.Controls;
using Everywhere.Extensions;
using Everywhere.Interfaces;
using Everywhere.Windows.Interop;

namespace Everywhere.Windows.Services;

public class Win32WindowHelper : IWindowHelper
{
    // ReSharper disable InconsistentNaming
    // ReSharper disable IdentifierTypo
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
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
            // if (msg is not (>= (uint)WINDOW_MESSAGE.WM_MOUSEMOVE and <= (uint)WINDOW_MESSAGE.WM_XBUTTONDBLCLK or (uint)WINDOW_MESSAGE.WM_NCHITTEST))
            //     Console.WriteLine($"{(WINDOW_MESSAGE)msg}\t{wparam}\t{lparam}");
            switch (msg)
            {
                case (uint)WINDOW_MESSAGE.WM_MOUSEACTIVATE:
                    handled = true;
                    return 3; // MA_NOACTIVATE;
                case (uint)WINDOW_MESSAGE.WM_ACTIVATE:
                case (uint)WINDOW_MESSAGE.WM_SETFOCUS:
                case (uint)WINDOW_MESSAGE.WM_KILLFOCUS:
                case (uint)WINDOW_MESSAGE.WM_ACTIVATEAPP:
                case (uint)WINDOW_MESSAGE.WM_NCACTIVATE:
                    handled = true;
                    return IntPtr.Zero;
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
            window.FocusManager?.ClearFocus(); // why, avalonia, why!!!!!!!!!!!!!!!!!!
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

    public void SetWindowHitTestInvisible(Window window)
    {
        window.Loaded += delegate
        {
            Win32Properties.AddWindowStylesCallback(window, WindowStylesCallback);

            static (uint style, uint exStyle) WindowStylesCallback(uint style, uint exStyle)
            {
                return (style, exStyle |
                    (uint)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW |
                    (uint)WINDOW_EX_STYLE.WS_EX_LAYERED |
                    (uint)WINDOW_EX_STYLE.WS_EX_TRANSPARENT);
            }

            if (window.TryGetPlatformHandle() is not { } handle) return;
            var hWnd = (HWND)handle.Handle;
            PInvoke.SetWindowLong(
                hWnd,
                WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
                PInvoke.GetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE) |
                (int)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW |
                (int)WINDOW_EX_STYLE.WS_EX_LAYERED |
                (int)WINDOW_EX_STYLE.WS_EX_TRANSPARENT);
            PInvoke.SetLayeredWindowAttributes(hWnd, new COLORREF(), 255, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);
        };
    }

    // Modify from WPF source code
    // https://referencesource.microsoft.com/#PresentationFramework/src/Framework/System/Windows/Shell/WindowChromeWorker.cs,de42dddb12b0ad8f
    public void SetWindowCornerRadius(Window window, CornerRadius cornerRadius)
    {
        var hWnd = window.TryGetPlatformHandle()?.Handle ?? 0;
        if (hWnd == 0) return;

        var bounds = window.Bounds;
        var scale = window.DesktopScaling;
        var size = new Size(bounds.Width * scale, bounds.Height * scale);
        if (size.Width <= 0d || size.Height <= 0d) return;

        scale *= 2;
        var shortestDimension = Math.Min(size.Width, size.Height);
        var topLeftRadius = Math.Min(cornerRadius.TopLeft * scale, shortestDimension / 2);

        var hRgn = new DeleteObjectSafeHandle();
        try
        {
            if (cornerRadius.IsUniform)
            {
                hRgn = CreateRoundRectRgn(new Rect(size), topLeftRadius);
            }
            else
            {
                // We need to combine HRGNs for each of the corners.
                // Create one for each quadrant, but let it overlap into the two adjacent ones
                // by the radius amount to ensure that there aren't corners etched into the middle
                // of the window.
                hRgn = CreateRoundRectRgn(new Rect(0, 0, size.Width / 2 + topLeftRadius, size.Height / 2 + topLeftRadius), topLeftRadius);

                var topRightRadius = Math.Min(cornerRadius.TopRight * scale, shortestDimension / 2);
                var topRightRegionRect = new Rect(
                    size.Width / 2 - topRightRadius,
                    0,
                    size.Width / 2 + topRightRadius,
                    size.Height / 2 + topRightRadius);

                using var _0 = CreateAndCombineRoundRectRgn(hRgn, topRightRegionRect, topRightRadius);

                var bottomLeftRadius = Math.Min(cornerRadius.BottomLeft * scale, shortestDimension / 2);
                var bottomLeftRegionRect = new Rect(
                    0,
                    size.Height / 2 - bottomLeftRadius,
                    size.Width / 2 + bottomLeftRadius,
                    size.Height / 2 + bottomLeftRadius);

                using var _1 = CreateAndCombineRoundRectRgn(hRgn, bottomLeftRegionRect, bottomLeftRadius);

                var bottomRightRadius = Math.Min(cornerRadius.BottomRight * scale, shortestDimension / 2);
                var bottomRightRegionRect = new Rect(
                    size.Width / 2 - bottomRightRadius,
                    size.Height / 2 - bottomRightRadius,
                    size.Width / 2 + bottomRightRadius,
                    size.Height / 2 + bottomRightRadius);

                using var _2 = CreateAndCombineRoundRectRgn(hRgn, bottomRightRegionRect, bottomRightRadius);
            }

            PInvoke.SetWindowRgn((HWND)hWnd, hRgn, window.IsVisible);
        }
        finally
        {
            hRgn.Dispose();
        }

        static DeleteObjectSafeHandle CreateRoundRectRgn(Rect region, double radius)
        {
            return radius.IsCloseTo(0d) ?
                PInvoke.CreateRectRgn_SafeHandle(
                    (int)Math.Floor(region.Left),
                    (int)Math.Floor(region.Top),
                    (int)Math.Ceiling(region.Right),
                    (int)Math.Ceiling(region.Bottom)) :
                PInvoke.CreateRoundRectRgn_SafeHandle(
                    (int)Math.Floor(region.Left),
                    (int)Math.Floor(region.Top),
                    (int)Math.Ceiling(region.Right) + 1,
                    (int)Math.Ceiling(region.Bottom) + 1,
                    (int)Math.Ceiling(radius),
                    (int)Math.Ceiling(radius));
        }

        static DeleteObjectSafeHandle CreateAndCombineRoundRectRgn(DeleteObjectSafeHandle hRgnSource, Rect region, double radius)
        {
            var hRgn = CreateRoundRectRgn(region, radius);
            if (PInvoke.CombineRgn(hRgnSource, hRgnSource, hRgn, RGN_COMBINE_MODE.RGN_OR) == GDI_REGION_TYPE.RGN_ERROR)
            {
                hRgn.Dispose();
                throw new InvalidOperationException("Unable to combine two HRGNs.");
            }

            return hRgn;
        }
    }

    public unsafe void HideWindowWithoutAnimation(Window window)
    {
        BOOL disableTransitions = true;
        var hWnd = window.TryGetPlatformHandle()?.Handle ?? 0;
        if (hWnd != 0)
        {
            PInvoke.DwmSetWindowAttribute(
                (HWND)hWnd,
                DWMWINDOWATTRIBUTE.DWMWA_TRANSITIONS_FORCEDISABLED,
                &disableTransitions,
                (uint)sizeof(BOOL));
        }
        window.Hide();
        if (hWnd != 0)
        {
            disableTransitions = false;
            PInvoke.DwmSetWindowAttribute(
                (HWND)hWnd,
                DWMWINDOWATTRIBUTE.DWMWA_TRANSITIONS_FORCEDISABLED,
                &disableTransitions,
                (uint)sizeof(BOOL));
        }
    }
}