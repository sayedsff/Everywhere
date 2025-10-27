using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using Avalonia;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Threading;
using Everywhere.Common;
using Everywhere.Extensions;
using Everywhere.I18N;
using Everywhere.Interop;
using Everywhere.Windows.Extensions;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Patterns.Infrastructure;
using FlaUI.UIA3;
using Microsoft.Extensions.Logging;
using Bitmap = Avalonia.Media.Imaging.Bitmap;
using IDataObject = System.Windows.IDataObject;
using INPUT = Windows.Win32.UI.Input.KeyboardAndMouse.INPUT;
using KEYBDINPUT = Windows.Win32.UI.Input.KeyboardAndMouse.KEYBDINPUT;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;
using Vector = Avalonia.Vector;

namespace Everywhere.Windows.Interop;

public partial class Win32VisualElementContext : IVisualElementContext
{
    private static readonly UIA3Automation Automation = new();
    private static readonly ITreeWalker TreeWalker = Automation.TreeWalkerFactory.GetContentViewWalker();

    public event IVisualElementContext.KeyboardFocusedElementChangedHandler? KeyboardFocusedElementChanged;

    public IVisualElement? KeyboardFocusedElement => TryFrom(Automation.FocusedElement);

    public IVisualElement? PointerOverElement => TryFrom(static () => PInvoke.GetCursorPos(out var point) ? Automation.FromPoint(point) : null);

    private readonly IWindowHelper _windowHelper;
    private readonly ILogger<Win32VisualElementContext> _logger;

    public Win32VisualElementContext(IWindowHelper windowHelper, ILogger<Win32VisualElementContext> logger)
    {
        _windowHelper = windowHelper;
        _logger = logger;
        // Automation.RegisterFocusChangedEvent(element =>
        // {
        //     if (KeyboardFocusedElementChanged is not { } handler) return;
        //     if (element == null)
        //     {
        //         handler(null);
        //         return;
        //     }
//
        //     handler(new AutomationVisualElementImpl(this, element, true));
        // });
    }


    public IVisualElement? ElementFromPoint(PixelPoint point, PickElementMode mode = PickElementMode.Element)
    {
        switch (mode)
        {
            case PickElementMode.Element:
            {
                return TryFrom(() => Automation.FromPoint(new Point(point.X, point.Y)));
            }
            case PickElementMode.Window:
            {
                IVisualElement? element = TryFrom(() => Automation.FromPoint(new Point(point.X, point.Y)), false);
                while (element is AutomationVisualElementImpl { IsTopLevelWindow: false })
                {
                    element = element.Parent;
                }

                return element;
            }
            case PickElementMode.Screen:
            {
                var hMonitor = PInvoke.MonitorFromPoint(new Point(point.X, point.Y), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
                return hMonitor == HMONITOR.Null ? null : new ScreenVisualElementImpl(this, hMonitor);
            }
        }

        return null;
    }

    public IVisualElement? ElementFromPointer(PickElementMode mode = PickElementMode.Element)
    {
        return !PInvoke.GetCursorPos(out var point) ? null : ElementFromPoint(new PixelPoint(point.X, point.Y), mode);
    }

    public Task<IVisualElement?> PickElementAsync(PickElementMode mode) =>
        ElementPickerWindow.PickAsync(this, _windowHelper, mode);

    private AutomationVisualElementImpl? TryFrom(Func<AutomationElement?> factory, bool windowBarrier = true)
    {
        try
        {
            if (factory() is { } element) return new AutomationVisualElementImpl(this, element, windowBarrier);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                new HandledException(ex, new DirectResourceKey("Failed to get AutomationElement")),
                "Failed to get AutomationElement");
        }

        return null;
    }

    private static bool IsAutomationException(Exception ex) =>
        ex.GetType().Namespace?.StartsWith("FlaUI.", StringComparison.Ordinal) == true;

    private static Bitmap CaptureScreen(PixelRect rect)
    {
        var gdiBitmap = new System.Drawing.Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(gdiBitmap))
        {
            graphics.CopyFromScreen(rect.X, rect.Y, 0, 0, new Size(rect.Width, rect.Height));
        }

        var data = gdiBitmap.LockBits(
            new Rectangle(0, 0, gdiBitmap.Width, gdiBitmap.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        Bitmap bitmap;
        try
        {
            bitmap = new Bitmap(
                Avalonia.Platform.PixelFormat.Bgra8888,
                AlphaFormat.Opaque,
                data.Scan0,
                rect.Size,
                new Vector(96d, 96d),
                data.Stride);
        }
        finally
        {
            gdiBitmap.UnlockBits(data);
        }

        return bitmap;
    }

    private class AutomationVisualElementImpl(
        Win32VisualElementContext context,
        AutomationElement element,
        bool windowBarrier
    ) : IVisualElement
    {
        public IVisualElementContext Context => context;

        public string Id { get; } = string.Join('.', element.Properties.RuntimeId.ValueOrDefault ?? []);

        public IVisualElement? Parent
        {
            get
            {
                try
                {
                    if (IsTopLevelWindow)
                    {
                        // this is a top level window
                        if (windowBarrier) return null;

                        var screen = PInvoke.MonitorFromWindow((HWND)NativeWindowHandle, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
                        return screen == HMONITOR.Null ? null : new ScreenVisualElementImpl(context, screen);
                    }

                    var parent = TreeWalker.GetParent(element);
                    return parent is null ? null : new AutomationVisualElementImpl(context, parent, windowBarrier);
                }
                catch (COMException)
                {
                    return null;
                }
            }
        }

        public IEnumerable<IVisualElement> Children
        {
            get
            {
                AutomationElement? child;
                try
                {
                    child = TreeWalker.GetFirstChild(element);
                }
                catch (COMException)
                {
                    yield break;
                }

                while (child is not null)
                {
                    yield return new AutomationVisualElementImpl(context, child, windowBarrier);

                    try
                    {
                        child = TreeWalker.GetNextSibling(child);
                    }
                    catch (COMException)
                    {
                        yield break;
                    }
                }
            }
        }

        public IVisualElement? PreviousSibling
        {
            get
            {
                if (windowBarrier && IsTopLevelWindow) return null;

                try
                {
                    var sibling = TreeWalker.GetPreviousSibling(element);
                    return sibling is null ? null : new AutomationVisualElementImpl(context, sibling, windowBarrier);
                }
                catch (COMException)
                {
                    return null;
                }
            }
        }

        public IVisualElement? NextSibling
        {
            get
            {
                if (windowBarrier && IsTopLevelWindow) return null;

                try
                {
                    var sibling = TreeWalker.GetNextSibling(element);
                    return sibling is null ? null : new AutomationVisualElementImpl(context, sibling, windowBarrier);
                }
                catch (COMException)
                {
                    return null;
                }
            }
        }

        public VisualElementType Type
        {
            get
            {
                try
                {
                    return element.Properties.ControlType.ValueOrDefault switch
                    {
                        ControlType.AppBar => VisualElementType.Menu,
                        ControlType.Button => VisualElementType.Button,
                        ControlType.Calendar => VisualElementType.Label,
                        ControlType.CheckBox => VisualElementType.CheckBox,
                        ControlType.ComboBox => VisualElementType.ComboBox,
                        ControlType.DataGrid => VisualElementType.DataGrid,
                        ControlType.DataItem => VisualElementType.DataGridItem,
                        ControlType.Document => VisualElementType.Document,
                        ControlType.Edit => VisualElementType.TextEdit,
                        ControlType.Group => VisualElementType.Panel,
                        ControlType.Header or ControlType.HeaderItem => VisualElementType.TableRow,
                        ControlType.Hyperlink => VisualElementType.Hyperlink,
                        ControlType.Image => VisualElementType.Image,
                        ControlType.List => VisualElementType.ListView,
                        ControlType.ListItem => VisualElementType.ListViewItem,
                        ControlType.Menu or ControlType.MenuBar => VisualElementType.Menu,
                        ControlType.MenuItem => VisualElementType.MenuItem,
                        ControlType.Pane => VisualElementType.TopLevel,
                        ControlType.ProgressBar => VisualElementType.ProgressBar,
                        ControlType.RadioButton => VisualElementType.RadioButton,
                        ControlType.ScrollBar => VisualElementType.ScrollBar,
                        ControlType.SemanticZoom => VisualElementType.ListView,
                        ControlType.Separator => VisualElementType.Unknown,
                        ControlType.Slider or ControlType.Spinner => VisualElementType.Slider,
                        ControlType.SplitButton => VisualElementType.Button,
                        ControlType.StatusBar => VisualElementType.Panel,
                        ControlType.Tab => VisualElementType.TabControl,
                        ControlType.TabItem => VisualElementType.TabItem,
                        ControlType.Table => VisualElementType.Table,
                        ControlType.Text => VisualElementType.Label,
                        ControlType.Thumb => VisualElementType.Slider,
                        ControlType.TitleBar or ControlType.ToolBar or ControlType.ToolTip => VisualElementType.Panel,
                        ControlType.Tree => VisualElementType.TreeView,
                        ControlType.TreeItem => VisualElementType.TreeViewItem,
                        ControlType.Window => VisualElementType.TopLevel,
                        _ => VisualElementType.Unknown
                    };
                }
                catch (COMException)
                {
                    return VisualElementType.Unknown;
                }
            }
        }

        public VisualElementStates States
        {
            get
            {
                try
                {
                    var states = VisualElementStates.None;
                    if (element.Properties.IsOffscreen.ValueOrDefault) states |= VisualElementStates.Offscreen;
                    if (!element.Properties.IsEnabled.ValueOrDefault) states |= VisualElementStates.Disabled;
                    if (element.Properties.HasKeyboardFocus.ValueOrDefault) states |= VisualElementStates.Focused;
                    if (element.Patterns.SelectionItem.TryGetPattern() is { IsSelected.ValueOrDefault: true })
                        states |= VisualElementStates.Selected;
                    if (element.Patterns.Value.TryGetPattern() is { IsReadOnly.ValueOrDefault: true }) states |= VisualElementStates.ReadOnly;
                    if (element.Properties.IsPassword.ValueOrDefault) states |= VisualElementStates.Password;
                    return states;
                }
                catch (COMException)
                {
                    return VisualElementStates.None;
                }
            }
        }

        public string? Name
        {
            get
            {
                try
                {
                    if (element.Properties.Name.TryGetValue(out var name)) return name;
                    if (element.Patterns.LegacyIAccessible.TryGetPattern() is { } accessiblePattern) return accessiblePattern.Name;
                    return null;
                }
                catch
                {
                    return null;
                }
            }
        }

        public PixelRect BoundingRectangle
        {
            get
            {
                try
                {
                    return element.BoundingRectangle.To(r => new PixelRect(
                        r.X,
                        r.Y,
                        r.Width,
                        r.Height));
                }
                catch (COMException)
                {
                    return default;
                }
            }
        }

        public int ProcessId { get; } = element.FrameworkAutomationElement.ProcessId.ValueOrDefault;

        public nint NativeWindowHandle { get; } = element.FrameworkAutomationElement.NativeWindowHandle.ValueOrDefault;

        public string? GetText(int maxLength = -1)
        {
            try
            {
                if (element.Patterns.Value.TryGetPattern() is { } valuePattern) return valuePattern.Value;
                if (element.Patterns.Text.TryGetPattern() is { } textPattern) return textPattern.DocumentRange.GetText(maxLength);
                if (element.Patterns.LegacyIAccessible.TryGetPattern() is { } accessiblePattern) return accessiblePattern.Value;
                return null;
            }
            catch
            {
                return null;
            }
        }

        private void EnsureFocusable()
        {
            try
            {
                element.Focus();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to focus element before sending shortcut.", ex);
            }
        }

        public void Invoke()
        {
            try
            {
                if (element.Patterns.Invoke.TryGetPattern() is { } invokePattern)
                {
                    invokePattern.Invoke();
                    return;
                }

                if (element.Patterns.Toggle.TryGetPattern() is { } togglePattern)
                {
                    togglePattern.Toggle();
                    return;
                }

                if (element.Patterns.SelectionItem.TryGetPattern() is { } selectionItemPattern)
                {
                    selectionItemPattern.Select();
                    return;
                }

                if (element.Patterns.ExpandCollapse.TryGetPattern() is { } expandCollapsePattern)
                {
                    var state = expandCollapsePattern.ExpandCollapseState.ValueOrDefault;
                    if (state is ExpandCollapseState.Collapsed or ExpandCollapseState.PartiallyExpanded)
                    {
                        expandCollapsePattern.Expand();
                    }
                    else
                    {
                        expandCollapsePattern.Collapse();
                    }

                    return;
                }

                if (element.Patterns.LegacyIAccessible.TryGetPattern() is { } legacyPattern)
                {
                    legacyPattern.DoDefaultAction();
                }
            }
            catch (COMException ex)
            {
                throw new InvalidOperationException("Failed to invoke the element through UI Automation.", ex);
            }
            catch (Exception ex) when (IsAutomationException(ex))
            {
                throw new InvalidOperationException("Failed to invoke the element through UI Automation.", ex);
            }

            throw new NotSupportedException("The target element does not expose an invoke-capable automation pattern.");
        }

        public void SetText(string text)
        {
            try
            {
                if (element.Patterns.Value.TryGetPattern() is { } valuePattern)
                {
                    if (valuePattern.IsReadOnly.ValueOrDefault)
                    {
                        throw new InvalidOperationException("The target element is read-only and cannot accept text.");
                    }

                    element.Focus();
                    new TextBox(element.FrameworkAutomationElement).Text = text;
                }
            }
            catch (COMException ex)
            {
                throw new InvalidOperationException("Failed to set text on the element through UI Automation.", ex);
            }
            catch (Exception ex) when (IsAutomationException(ex))
            {
                throw new InvalidOperationException("Failed to set text on the element through UI Automation.", ex);
            }

            throw new NotSupportedException("The target element does not support programmatic text input.");
        }

        public void SendShortcut(KeyboardShortcut shortcut)
        {
            EnsureFocusable();

            // Use PInvoke.SendInput to send the shortcut to the focused element.
            var inputs = new List<INPUT>();
            if (shortcut.Modifiers.HasFlag(KeyModifiers.Control)) MakeInputs(VIRTUAL_KEY.VK_CONTROL);
            if (shortcut.Modifiers.HasFlag(KeyModifiers.Alt)) MakeInputs(VIRTUAL_KEY.VK_MENU);
            if (shortcut.Modifiers.HasFlag(KeyModifiers.Shift)) MakeInputs(VIRTUAL_KEY.VK_SHIFT);
            if (shortcut.Modifiers.HasFlag(KeyModifiers.Meta)) MakeInputs(VIRTUAL_KEY.VK_LWIN);
            MakeInputs(shortcut.Key.ToVirtualKey());

            var result = PInvoke.SendInput(CollectionsMarshal.AsSpan(inputs), Marshal.SizeOf<INPUT>());
            if (result == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to send keyboard input to the target element.");
            }

            void MakeInputs(VIRTUAL_KEY vk)
            {
                inputs.InsertRange(
                    inputs.Count / 2,
                    [
                        new INPUT
                        {
                            type = INPUT_TYPE.INPUT_KEYBOARD,
                            Anonymous = new INPUT._Anonymous_e__Union
                            {
                                ki = new KEYBDINPUT
                                {
                                    wVk = vk,
                                    dwFlags = 0,
                                }
                            }
                        },
                        new INPUT
                        {
                            type = INPUT_TYPE.INPUT_KEYBOARD,
                            Anonymous = new INPUT._Anonymous_e__Union
                            {
                                ki = new KEYBDINPUT
                                {
                                    wVk = vk,
                                    dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP,
                                }
                            }
                        },
                    ]);
            }
        }

        public string? GetSelectionText()
        {
            try
            {
                // 1) Prefer UIA TextPattern selection text
                if (element.Patterns.Text.TryGetPattern() is { } textPattern)
                {
                    var ranges = textPattern.GetSelection();
                    if (ranges is { Length: > 0 })
                    {
                        var selected = string.Join(null, ranges.Select(r => r.GetText(-1)));
                        if (!string.IsNullOrEmpty(selected))
                            return selected;
                    }
                }

                // 2) Fallback to SelectionItemPattern (if selected, return element's text)
                if (element.Patterns.SelectionItem.TryGetPattern() is { } selectionItemPattern)
                {
                    if (selectionItemPattern.IsSelected.ValueOrDefault)
                    {
                        var v = GetText();
                        if (!string.IsNullOrEmpty(v))
                            return v;
                    }
                }

                // TODO: Following method takes no effect QAQ
                // 3) Last resort: send WM_COPY to the focused child window of target thread, then wait for clipboard update
                if (!TryGetWindow(element, out var topLevel) || topLevel == 0)
                    return null;

                var hTop = (HWND)topLevel;

                // Resolve the real focused child HWND in the target GUI thread
                var target = hTop;
                uint targetTid;
                unsafe { targetTid = PInvoke.GetWindowThreadProcessId(hTop); }
                var currentTid = PInvoke.GetCurrentThreadId();
                var attached = false;
                try
                {
                    attached = PInvoke.AttachThreadInput(currentTid, targetTid, true);
                    var hFocus = PInvoke.GetFocus();
                    if (hFocus != HWND.Null)
                        target = hFocus;
                }
                finally
                {
                    if (attached)
                        _ = PInvoke.AttachThreadInput(currentTid, targetTid, false);
                }

                // Read clipboard text (best effort)
                string? result = null;
                Dispatcher.UIThread.Invoke(() =>
                {
                    // Backup current clipboard (best effort, avoid user-visible side effects)
                    IDataObject? backup = null;
                    try
                    {
                        // backup = Clipboard.GetDataObject();
                    }
                    catch
                    {
                        /* ignore */
                    }

                    // Arm the clipboard listener before sending WM_COPY to avoid race
                    var listener = ClipboardListener.Shared;
                    listener.BeginWait();

                    // Ask target control to copy selection without simulating Ctrl+C
                    PInvoke.SendMessage(target, (uint)WINDOW_MESSAGE.WM_COPY, 0, 0);

                    // Wait for WM_CLIPBOARDUPDATE (timeout ~50ms)
                    if (!listener.WaitNextUpdate(50)) return;

                    try
                    {
                        if (Clipboard.ContainsText())
                        {
                            result = Clipboard.GetText();
                        }
                    }
                    catch
                    {
                        /* ignore */
                    }

                    // Restore clipboard
                    if (backup != null)
                    {
                        try
                        {
                            Clipboard.SetDataObject(backup, true);
                        }
                        catch
                        {
                            /* ignore */
                        }
                    }
                });

                return string.IsNullOrEmpty(result) ? null : result;
            }
            catch
            {
                return null;
            }
        }

        // BUG: For a minimized window, the captured image is buggy (but child elements are fine).
        public Task<Bitmap> CaptureAsync()
        {
            var rect = BoundingRectangle;
            if (rect.Width <= 0 || rect.Height <= 0)
                throw new InvalidOperationException("Cannot capture an element with zero width or height.");

            if (!TryGetWindow(element, out var hWnd) ||
                (hWnd = PInvoke.GetAncestor((HWND)hWnd, GET_ANCESTOR_FLAGS.GA_ROOTOWNER)) == 0)
                throw new InvalidOperationException("Cannot capture an element without a valid window handle.");

            if (!PInvoke.GetWindowRect((HWND)hWnd, out var windowRect))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            return Direct3D11ScreenCapture.CaptureAsync(
                hWnd,
                new PixelRect(
                    rect.X - windowRect.X,
                    rect.Y - windowRect.Y,
                    rect.Width,
                    rect.Height));
        }

        #region Interop

        private static bool TryGetWindow(AutomationElement? element, out nint hWnd)
        {
            while (element != null)
            {
                if (element.FrameworkAutomationElement.NativeWindowHandle.TryGetValue(out hWnd))
                {
                    return true;
                }

                element = TreeWalker.GetParent(element);
            }

            hWnd = 0;
            return false;
        }

        /// <summary>
        ///     Determines if the current element is a top-level window in a Win32 context.
        /// </summary>
        /// <remarks>
        ///     e.g. A control inside a window or a non-win32 element will return false.
        /// </remarks>
        public bool IsTopLevelWindow =>
            NativeWindowHandle != IntPtr.Zero &&
            PInvoke.GetAncestor((HWND)NativeWindowHandle, GET_ANCESTOR_FLAGS.GA_ROOTOWNER) == NativeWindowHandle;

        #endregion

        public override bool Equals(object? obj)
        {
            if (obj is not AutomationVisualElementImpl other) return false;
            return Id == other.Id;
        }

        public override int GetHashCode() => Id.GetHashCode();

        public override string ToString() => $"({Id}) [{element.ControlType}] {Name} - {GetText(128)}";
    }

    private unsafe class ScreenVisualElementImpl(Win32VisualElementContext context, HMONITOR hMonitor) : IVisualElement
    {
        public IVisualElementContext Context => context;

        public string Id => $"Screen {hMonitor}";

        public IVisualElement? Parent => null;

        /// <summary>
        /// Gets all windows on the screen.
        /// </summary>
        public IEnumerable<IVisualElement> Children
        {
            get
            {
                var windows = new List<HWND>();
                PInvoke.EnumWindows(
                    (hWnd, _) =>
                    {
                        if (PInvoke.GetAncestor(hWnd, GET_ANCESTOR_FLAGS.GA_ROOTOWNER) != hWnd) return true; // ignore child windows
                        if (!PInvoke.IsWindowVisible(hWnd)) return true;

                        var windowPlacement = new WINDOWPLACEMENT();
                        if (!PInvoke.GetWindowPlacement(hWnd, ref windowPlacement) ||
                            windowPlacement.showCmd == SHOW_WINDOW_CMD.SW_SHOWMINIMIZED) return true;

                        if (PInvoke.MonitorFromWindow(hWnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONULL) != hMonitor) return true;
                        windows.Add(hWnd);
                        return true;
                    },
                    0);
                return windows.Select(h => context.TryFrom(() => Automation.FromHandle(h), false)).OfType<IVisualElement>();
            }
        }

        public IVisualElement? PreviousSibling
        {
            get
            {
                var previousMonitor = HMONITOR.Null;
                var result = HMONITOR.Null;
                var hDc = PInvoke.GetDC(HWND.Null);
                try
                {
                    PInvoke.EnumDisplayMonitors(
                        hDc,
                        null,
                        (hm, _, _, _) =>
                        {
                            if (hm == hMonitor)
                            {
                                result = previousMonitor;
                                return false;
                            }

                            previousMonitor = hm;
                            return true;
                        },
                        default);
                }
                finally
                {
                    _ = PInvoke.ReleaseDC(HWND.Null, hDc);
                }

                return result != HMONITOR.Null ? new ScreenVisualElementImpl(context, result) : null;
            }
        }

        public IVisualElement? NextSibling
        {
            get
            {
                var previousMonitor = HMONITOR.Null;
                var result = HMONITOR.Null;
                var hDc = PInvoke.GetDC(HWND.Null);
                try
                {
                    PInvoke.EnumDisplayMonitors(
                        hDc,
                        null,
                        (hm, _, _, _) =>
                        {
                            if (previousMonitor == hMonitor)
                            {
                                result = hm;
                                return false;
                            }

                            previousMonitor = hm;
                            return true;
                        },
                        default);
                }
                finally
                {
                    _ = PInvoke.ReleaseDC(HWND.Null, hDc);
                }

                return result != HMONITOR.Null ? new ScreenVisualElementImpl(context, result) : null;
            }
        }

        public VisualElementType Type => VisualElementType.Screen;

        public VisualElementStates States => VisualElementStates.None;

        public string? Name => null;

        public PixelRect BoundingRectangle
        {
            get
            {
                var mi = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
                return PInvoke.GetMonitorInfo(hMonitor, ref mi) ?
                    new PixelRect(
                        mi.rcMonitor.X,
                        mi.rcMonitor.Y,
                        mi.rcMonitor.Width,
                        mi.rcMonitor.Height) :
                    default;
            }
        }

        public int ProcessId => 0;

        public nint NativeWindowHandle => 0;

        public string? GetText(int maxLength = -1) => null;

        public void Invoke() { } // no-op

        public void SetText(string text) { } // no-op

        public void SendShortcut(KeyboardShortcut shortcut) { } // no-op

        public string? GetSelectionText() => null;

        public Task<Bitmap> CaptureAsync()
        {
            return Task.FromResult(CaptureScreen(BoundingRectangle));
        }
    }
}

public static class AutomationExtension
{
    /// <summary>
    /// Sometimes pattern.TryGetPattern() will throw an exception!?
    /// </summary>
    /// <param name="pattern"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T? TryGetPattern<T>(this IAutomationPattern<T> pattern) where T : class, IPattern
    {
        try
        {
            return pattern.PatternOrDefault;
        }
        catch
        {
            return null;
        }
    }
}