using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Everywhere.Interfaces;
using Everywhere.Windows.Interop;
using MicroCom.Runtime;
using Visual = Windows.UI.Composition.Visual;

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
            if (e.Property != Avalonia.Visual.IsVisibleProperty || e.NewValue is not false) return;
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

    private readonly Dictionary<nint, CompositionContext> compositionContexts = [];

    // Modify from WPF source code
    // https://referencesource.microsoft.com/#PresentationFramework/src/Framework/System/Windows/Shell/WindowChromeWorker.cs,de42dddb12b0ad8f
    public void SetWindowCornerRadius(Window window, CornerRadius cornerRadius)
    {
        // Ensure dispatcher queue
        Dispatcher.UIThread.VerifyAccess();

        var hWnd = window.TryGetPlatformHandle()?.Handle ?? 0;
        if (hWnd == 0) return;

        Compositor compositor;
        Visual rootVisual;
        if (!compositionContexts.TryGetValue(hWnd, out var compositionContext))
        {
            // we will need lots of hacks, let's go
            if (window.PlatformImpl?.GetType().GetField("_glSurface", BindingFlags.Instance | BindingFlags.NonPublic) is not { } glSurfaceField) return;
            if (glSurfaceField.GetValue(window.PlatformImpl) is not { } glSurface) return;
            if (glSurface.GetType().GetField("_window", BindingFlags.Instance | BindingFlags.NonPublic) is not { } windowField) return;
            if (windowField.GetValue(glSurface) is not { } compositedWindow) return; // Avalonia.Win32.WinRT.Composition.WinUiCompositedWindow
            if (compositedWindow.GetType().GetField("_shared", BindingFlags.Instance | BindingFlags.NonPublic) is not { } sharedField) return;
            if (sharedField.GetValue(compositedWindow) is not { } shared) return; // Avalonia.Win32.WinRT.Composition.WinUiCompositionShared
            if (shared.GetType().GetProperty("Compositor", BindingFlags.Instance | BindingFlags.Public) is not { } compositorProperty) return;
            if (compositorProperty.GetValue(shared) is not MicroComProxyBase avaloniaCompositor) return; // Avalonia.Win32.WinRT.ICompositor
            if (compositedWindow.GetType().GetField("_target", BindingFlags.Instance | BindingFlags.NonPublic) is not { } targetField) return;
            if (targetField.GetValue(compositedWindow) is not MicroComProxyBase avaloniaTarget) return; // Avalonia.Win32.WinRT.ICompositionTarget

            compositor = Compositor.FromAbi(avaloniaCompositor.NativePointer);
            var target = DesktopWindowTarget.FromAbi(avaloniaTarget.NativePointer);
            compositionContexts[hWnd] = compositionContext = new CompositionContext(compositor, rootVisual = target.Root);

            // var previousDesktopScaling = window.DesktopScaling;
            // window.PositionChanged += delegate
            // {
            //     if (previousDesktopScaling.IsCloseTo(window.DesktopScaling)) return;
            //
            //     previousDesktopScaling = window.DesktopScaling;
            //     Debug.WriteLine(previousDesktopScaling);
            //     SetWindowCornerRadiusInternal();
            // };

            window.SizeChanged += delegate
            {
                SetWindowCornerRadiusInternal();
            };

            window.Closed += delegate
            {
                compositionContext.Clip?.Dispose();
                compositionContexts.Remove(hWnd);
            };
        }
        else
        {
            (compositor, rootVisual) = compositionContext;
        }

        SetWindowCornerRadiusInternal();

        void SetWindowCornerRadiusInternal()
        {
            var bounds = window.Bounds;
            var scale = window.DesktopScaling;
            var width = (float)(bounds.Width * scale);
            var height = (float)(bounds.Height * scale);
            if (width <= 0d || height <= 0d) return;

            compositionContext.Clip?.Dispose();

            var cornerRadiusLimit = Math.Min(width, height) / 2d;
            var topLeft = (float)Math.Min(cornerRadius.TopLeft * scale, cornerRadiusLimit);
            if (cornerRadius.IsUniform)
            {
                using var rectGeometry = compositor.CreateRoundedRectangleGeometry();
                rectGeometry.Size = new Vector2(width, height);
                rectGeometry.CornerRadius = new Vector2(topLeft, topLeft);
                rootVisual.Clip = compositionContext.Clip = compositor.CreateGeometricClip(rectGeometry);
            }
            else
            {
                CreateComplexRoundedRectangleCompositionPath(
                    width,
                    height,
                    topLeft,
                    (float)Math.Min(cornerRadius.TopRight * scale, cornerRadiusLimit),
                    (float)Math.Min(cornerRadius.BottomRight * scale, cornerRadiusLimit),
                    (float)Math.Min(cornerRadius.BottomLeft * scale, cornerRadiusLimit),
                    out var pathGeometryPtr).ThrowOnFailure();
                var pathGeometry = CompositionPath.FromAbi(pathGeometryPtr);
                using var compositionGeometry = compositor.CreatePathGeometry(pathGeometry);
                rootVisual.Clip = compositionContext.Clip = compositor.CreateGeometricClip(compositionGeometry);
            }
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

    private record CompositionContext(Compositor Compositor, Visual RootVisual)
    {
        public CompositionClip? Clip { get; set; }
    }

    [DllImport("Everywhere.Windows.InteropHelper.dll", ExactSpelling = true)]
    private static extern HRESULT CreateComplexRoundedRectangleCompositionPath(
        float width,
        float height,
        float topLeft,
        float topRight,
        float bottomRight,
        float bottomLeft,
        out nint pCompositionPath);
}
