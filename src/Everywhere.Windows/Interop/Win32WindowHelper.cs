using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;
using Avalonia.Controls;
using Everywhere.Interop;

namespace Everywhere.Windows.Interop;

/// <summary>
/// Reference: Powertoys
/// </summary>
public class Win32WindowHelper : IWindowHelper
{
    public void SetFocusable(Window window, bool focusable)
    {
        if (window.TryGetPlatformHandle() is not { } handle) return;
        var windowLong = PInvoke.GetWindowLong((HWND)handle.Handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

        if (focusable)
        {
            Win32Properties.AddWindowStylesCallback(window, WindowStylesCallback);
            Win32Properties.AddWndProcHookCallback(window, WndProcHookCallback);

            PInvoke.SetWindowLong(
                (HWND)handle.Handle,
                WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
                windowLong & ~(
                    (int)WINDOW_EX_STYLE.WS_EX_NOACTIVATE |
                    (int)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW));
        }
        else
        {
            Win32Properties.RemoveWindowStylesCallback(window, WindowStylesCallback);
            Win32Properties.RemoveWndProcHookCallback(window, WndProcHookCallback);

            PInvoke.SetWindowLong(
                (HWND)handle.Handle,
                WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
                windowLong |
                (int)WINDOW_EX_STYLE.WS_EX_NOACTIVATE |
                (int)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW);
        }

        static (uint style, uint exStyle) WindowStylesCallback(uint style, uint exStyle)
        {
            return (style, exStyle |
                (uint)WINDOW_EX_STYLE.WS_EX_NOACTIVATE |
                (uint)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW);
        }

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
    }

    public void SetHitTestVisible(Window window, bool visible)
    {
        if (window.TryGetPlatformHandle() is not { } handle) return;
        var windowLong = PInvoke.GetWindowLong((HWND)handle.Handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

        if (visible)
        {
            Win32Properties.RemoveWindowStylesCallback(window, WindowStylesCallback);

            PInvoke.SetWindowLong(
                (HWND)handle.Handle,
                WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
                windowLong & ~(
                    (int)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW |
                    (int)WINDOW_EX_STYLE.WS_EX_LAYERED |
                    (int)WINDOW_EX_STYLE.WS_EX_TRANSPARENT));
        }
        else
        {
            Win32Properties.AddWindowStylesCallback(window, WindowStylesCallback);

            PInvoke.SetWindowLong(
                (HWND)handle.Handle,
                WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
                windowLong |
                (int)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW |
                (int)WINDOW_EX_STYLE.WS_EX_LAYERED |
                (int)WINDOW_EX_STYLE.WS_EX_TRANSPARENT);
            PInvoke.SetLayeredWindowAttributes((HWND)handle.Handle, new COLORREF(), 255, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);
        }

        static (uint style, uint exStyle) WindowStylesCallback(uint style, uint exStyle)
        {
            return (style, exStyle |
                (uint)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW |
                (uint)WINDOW_EX_STYLE.WS_EX_LAYERED |
                (uint)WINDOW_EX_STYLE.WS_EX_TRANSPARENT);
        }
    }

    public bool GetEffectiveVisible(Window window)
    {
        var isVisible = window.IsVisible;

        if (window.TryGetPlatformHandle() is not { } handle) return isVisible;

        unsafe
        {
            // We need to check if our window is cloaked or not. A cloaked window is still
            // technically visible, because SHOW/HIDE != iconic (minimized) != cloaked
            // (these are all separate states)
            long attr = 0;
            PInvoke.DwmGetWindowAttribute((HWND)handle.Handle, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &attr, sizeof(long));
            if (attr == 1 /* DWM_CLOAKED_APP */)
            {
                isVisible = false;
            }
        }

        return isVisible;
    }

    public void SetCloaked(Window window, bool cloaked)
    {
        if (window.TryGetPlatformHandle() is not { } handle) return;
        var hWnd = (HWND)handle.Handle;
        if (cloaked)
        {
            cloaked = Cloak(hWnd);

            // Then hide our HWND, to make sure that the OS gives the FG / focus back to another app
            // (there's no way for us to guess what the right hwnd might be, only the OS can do it right)
            PInvoke.ShowWindow(hWnd, SHOW_WINDOW_CMD.SW_HIDE);

            if (cloaked)
            {
                // TRICKY: show our HWND again. This will trick XAML into painting our
                // HWND again, so that we avoid the "flicker" caused by a WinUI3 app
                // window being first shown
                // SW_SHOWNA will prevent us for trying to fight the focus back
                PInvoke.ShowWindow(hWnd, SHOW_WINDOW_CMD.SW_SHOWNA);

                // Intentionally leave the window cloaked. So our window is "visible",
                // but also cloaked, so you can't see it.

                // If the window was not cloaked, then leave it hidden.
                // Sure, it's not ideal, but at least it's not visible.
            }
        }
        else
        {
            // Remember, IsIconic == "minimized", which is entirely different state
            // from "show/hide"
            // If we're currently minimized, restore us first, before we reveal
            // our window. Otherwise, we'd just be showing a minimized window -
            // which would remain not visible to the user.
            if (PInvoke.IsIconic(hWnd))
            {
                // Make sure our HWND is cloaked before any possible window manipulations
                Cloak(hWnd);

                PInvoke.ShowWindow(hWnd, SHOW_WINDOW_CMD.SW_RESTORE);
            }

            // Just to be sure, SHOW our hwnd.
            window.Show();

            // Once we're done, uncloak to avoid all animations
            Uncloak(hWnd);

            PInvoke.SetForegroundWindow(hWnd);
            PInvoke.SetActiveWindow(hWnd);
            PInvoke.SetFocus(hWnd);

            // Push our window to the top of the Z-order and make it the topmost, so that it appears above all other windows.
            // We want to remove the topmost status when we hide the window (because we cloak it instead of hiding it).
            PInvoke.SetWindowPos(hWnd, HWND.HWND_TOPMOST, 0, 0, 0, 0, SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE);
        }
    }

    private static bool Cloak(HWND hWnd)
    {
        bool wasCloaked;
        unsafe
        {
            BOOL value = true;
            var hr = PInvoke.DwmSetWindowAttribute(hWnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAK, &value, (uint)sizeof(BOOL));
            wasCloaked = hr.Succeeded;
        }

        if (wasCloaked)
        {
            // Because we're only cloaking the window, bury it at the bottom in case something can
            // see it - e.g. some accessibility helper (note: this also removes the top-most status).
            PInvoke.SetWindowPos(hWnd, HWND.HWND_BOTTOM, 0, 0, 0, 0, SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE);
        }

        return wasCloaked;
    }

    private unsafe static void Uncloak(HWND hWnd)
    {
        BOOL value = false;
        PInvoke.DwmSetWindowAttribute(hWnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAK, &value, (uint)sizeof(BOOL));
    }
}