using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Everywhere.Extensions;
using Everywhere.Interfaces;
using Everywhere.Windows.Utils;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Patterns.Infrastructure;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using ZLinq;
using Application = Avalonia.Application;
using Bitmap = Avalonia.Media.Imaging.Bitmap;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace Everywhere.Windows.Services;

public partial class Win32VisualElementContext : IVisualElementContext
{
    // private const string PipeName = "everywhere_text_service";

    private static readonly UIA3Automation Automation = new();
    private static readonly ITreeWalker TreeWalker = Automation.TreeWalkerFactory.GetContentViewWalker();

    // private static readonly TextServiceImpl TextService = new();
    private static readonly int CurrentProcessId = (int)PInvoke.GetCurrentProcessId();
    // private static readonly Lazy<DwmScreenCaptureHelper> DwmScreenCaptureHelper = new();
    private static readonly Lazy<Direct3D11ScreenCaptureHelper> Direct3D11ScreenCaptureHelper = new();

    public event IVisualElementContext.KeyboardFocusedElementChangedHandler? KeyboardFocusedElementChanged;

    public IVisualElement? KeyboardFocusedElement => TryFrom(Automation.FocusedElement);

    public IVisualElement? PointerOverElement => TryFrom(static () => PInvoke.GetCursorPos(out var point) ? Automation.FromPoint(point) : null);

    private readonly INativeHelper nativeHelper;

    public Win32VisualElementContext(INativeHelper nativeHelper)
    {
        this.nativeHelper = nativeHelper;

        Automation.RegisterFocusChangedEvent(element =>
        {
            if (KeyboardFocusedElementChanged is not { } handler) return;
            if (element == null)
            {
                handler(null);
                return;
            }

            if (element.FrameworkAutomationElement.ProcessId.ValueOrDefault == CurrentProcessId) return;
            handler(new AutomationVisualElementImpl(this, element, true));
        });
    }

    public IVisualElement? ElementFromPoint(PixelPoint point) => TryFrom(() => Automation.FromPoint(new Point(point.X, point.Y)));

    public async Task<IVisualElement?> PickElementAsync(PickElementMode mode)
    {
        if (Application.Current is not { ApplicationLifetime: ClassicDesktopStyleApplicationLifetime desktopLifetime })
        {
            return null;
        }

        var windows = desktopLifetime.Windows.AsValueEnumerable().Where(w => w.IsVisible).ToList();
        foreach (var window in windows) nativeHelper.HideWindowWithoutAnimation(window);
        var result = await ElementPicker.PickAsync(this, nativeHelper, mode);
        foreach (var window in windows) window.IsVisible = true;
        return result;
    }

    private AutomationVisualElementImpl? TryFrom(Func<AutomationElement?> factory, bool windowBarrier = true)
    {
        try
        {
            if (factory() is { } element && element.FrameworkAutomationElement.ProcessId.ValueOrDefault != CurrentProcessId)
                return new AutomationVisualElementImpl(this, element, windowBarrier);
        }
        catch (Exception ex)
        {
            // Log the exception if needed
            Console.WriteLine($"Error retrieving UI Automation element: {ex.Message}");
        }

        return null;
    }

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
                        ControlType.Header => VisualElementType.TableRow,
                        ControlType.HeaderItem => VisualElementType.TableRow,
                        ControlType.Hyperlink => VisualElementType.Hyperlink,
                        ControlType.Image => VisualElementType.Image,
                        ControlType.List => VisualElementType.ListView,
                        ControlType.ListItem => VisualElementType.ListViewItem,
                        ControlType.Menu => VisualElementType.Menu,
                        ControlType.MenuBar => VisualElementType.Menu,
                        ControlType.MenuItem => VisualElementType.MenuItem,
                        ControlType.Pane => VisualElementType.TopLevel,
                        ControlType.ProgressBar => VisualElementType.ProgressBar,
                        ControlType.RadioButton => VisualElementType.RadioButton,
                        ControlType.ScrollBar => VisualElementType.ScrollBar,
                        ControlType.SemanticZoom => VisualElementType.ListView,
                        ControlType.Separator => VisualElementType.Unknown,
                        ControlType.Slider => VisualElementType.Slider,
                        ControlType.Spinner => VisualElementType.Slider,
                        ControlType.SplitButton => VisualElementType.Button,
                        ControlType.StatusBar => VisualElementType.Panel,
                        ControlType.Tab => VisualElementType.TabControl,
                        ControlType.TabItem => VisualElementType.TabItem,
                        ControlType.Table => VisualElementType.Table,
                        ControlType.Text => VisualElementType.Label,
                        ControlType.Thumb => VisualElementType.Slider,
                        ControlType.TitleBar => VisualElementType.Panel,
                        ControlType.ToolBar => VisualElementType.Panel,
                        ControlType.ToolTip => VisualElementType.Panel,
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

        public void SetText(string text, bool append)
        {
            if (States.HasFlag(VisualElementStates.Disabled | VisualElementStates.ReadOnly))
            {
                throw new InvalidOperationException("Cannot set text on a disabled or read-only element.");
            }

            try
            {
                element.Focus();

                var pid = element.FrameworkAutomationElement.ProcessId.ValueOrDefault;
                if (TryGetWindow(element, out var hWnd) || TryGetWindow((uint)pid, out hWnd))
                {
                    PInvoke.SetForegroundWindow(new HWND(hWnd));
                }
                else
                {
                    throw new InvalidOperationException("Cannot set text on an element without a valid window handle or process ID.");
                }

                _ = TrySetValueWithValuePattern() ||
                    // TrySetValueWithTextService() ||
                    TrySetValueWithSendInput();
            }
            catch (COMException) { }

            bool TrySetValueWithValuePattern()
            {
                if (element.Patterns.Value.TryGetPattern() is not { } valuePattern) return false;

                try
                {
                    var finalText = append ? valuePattern.Value + text : text;
                    valuePattern.SetValue(finalText);
                    return valuePattern.Value.ValueOrDefault == finalText;
                }
                catch
                {
                    return false;
                }
            }

            // bool TrySetValueWithTextService()
            // {
            //     TextService.SendAsync(
            //         new ServerMessage
            //         {
            //             SetFocusText = new SetFocusText
            //             {
            //                 Text = text,
            //                 Append = append
            //             }
            //         },
            //         (uint)pid,
            //         (HWND)hWnd); // todo: async
            //
            //     return true;
            // }

            bool TrySetValueWithSendInput()
            {
                if (element.Properties.IsKeyboardFocusable)
                {
                    Retry.WhileFalse(() => element.Properties.HasKeyboardFocus, TimeSpan.FromSeconds(0.5));
                }

                if (!append)
                {
                    // TODO: clear the text first
                }
                SendUnicodeString(text);
                return true;
            }
        }

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

            return Direct3D11ScreenCaptureHelper.Value.CaptureAsync(
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

        private unsafe static bool TryGetWindow(uint pid, out nint hWnd)
        {
            if (pid == 0)
            {
                hWnd = 0;
                return false;
            }

            var result = new EnumWindowsParams
            {
                pid = pid
            };
            PInvoke.EnumWindows(
                static (p0, p1) =>
                {
                    var pParams = (EnumWindowsParams*)p0;
                    if (p1 != pParams->pid) return true;
                    pParams->hWnd = p0;
                    return false;
                },
                new IntPtr(&result));

            hWnd = result.hWnd;
            return hWnd != 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct EnumWindowsParams
        {
            public nint hWnd;
            public uint pid;
        }

        private unsafe static void SendUnicodeString(string text)
        {
            var inputs = new Span<INPUT>(new INPUT[text.Length * 2]);

            for (var i = 0; i < text.Length; i++)
            {
                ref var inputDown = ref inputs[i];
                ref var inputUp = ref inputs[i + 1];

                inputDown.type = inputUp.type = INPUT_TYPE.INPUT_KEYBOARD;
                inputDown.Anonymous.ki.wScan = inputUp.Anonymous.ki.wScan = text[i];
                inputDown.Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE;
                inputUp.Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;
            }

            var inputBlocked = PInvoke.BlockInput(true);
            PInvoke.SendInput(inputs, sizeof(INPUT));
            if (inputBlocked) PInvoke.BlockInput(false);
        }

        /// <summary>
        ///     Determines if the current element is a top-level window in a Win32 context.
        /// </summary>
        /// <remarks>
        ///     e.g. A control inside a window or a non-win32 element will return false.
        /// </remarks>
        private bool IsTopLevelWindow =>
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

                        var pid = 0U;
                        if (PInvoke.GetWindowThreadProcessId(hWnd, &pid) == 0 || pid == CurrentProcessId) return true;

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

        public void SetText(string text, bool append) { }

        public Task<Bitmap> CaptureAsync()
        {
            return Task.FromResult(CaptureScreen(BoundingRectangle));
        }
    }

    // private class TextServiceImpl : IDisposable
    // {
    //     private readonly CancellationTokenSource cancellationTokenSource = new();
    //     private readonly ConcurrentDictionary<uint, NamedPipeServerStream> clientStreams = new();
    //
    //     public TextServiceImpl()
    //     {
    //         Task.Run(() => HostAsync(cancellationTokenSource.Token));
    //     }
    //
    //     public async Task<bool> SendAsync(ServerMessage message, uint pid, HWND hWnd, CancellationToken cancellationToken = default)
    //     {
    //         if (pid == 0) pid = GetProcessId(hWnd);
    //         if (pid == 0) return false;
    //
    //         if (!clientStreams.TryGetValue(pid, out var stream))
    //         {
    //             await ActivateTextServiceOnTargetWindow(hWnd);
    //
    //             for (var i = 0; i < 5; i++)
    //             {
    //                 await Task.Delay(100 * (2 * i + 1), cancellationToken);
    //                 if (clientStreams.TryGetValue(pid, out stream)) break;
    //             }
    //
    //             if (stream == null) return false; // Failed to connect to the client
    //         }
    //
    //         var data = message.ToByteArray();
    //         await stream.WriteAsync(data, cancellationToken);
    //         return true;
    //     }
    //
    //     private async Task HostAsync(CancellationToken cancellationToken)
    //     {
    //         Debug.WriteLine("Starting NamedPipeServerStream...");
    //         while (!cancellationToken.IsCancellationRequested)
    //         {
    //             var stream = new NamedPipeServerStream(
    //                 PipeName,
    //                 PipeDirection.InOut,
    //                 NamedPipeServerStream.MaxAllowedServerInstances,
    //                 PipeTransmissionMode.Message,
    //                 PipeOptions.Asynchronous);
    //             await stream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
    //
    //             Debug.WriteLine("Client connected, waiting for initialization...");
    //             uint pid;
    //             try
    //             {
    //                 var buffer = new byte[4096];
    //                 var bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    //                 if (bytesRead == 0) throw new InvalidDataException("Client disconnected before sending data.");
    //
    //                 var message = ClientMessage.Parser.ParseFrom(buffer.AsSpan(0, bytesRead));
    //                 if (message.DataCase != ClientMessage.DataOneofCase.Initialized)
    //                 {
    //                     throw new InvalidDataException("Invalid message received when waiting for initialization.");
    //                 }
    //
    //                 pid = message.Initialized.Pid;
    //                 Debug.WriteLine($"[{pid}] Client Initialized");
    //             }
    //             catch
    //             {
    //                 await stream.DisposeAsync();
    //                 throw;
    //             }
    //
    //             Task.Run(() => ReadAsync(pid, stream, cancellationToken), cancellationToken)
    //                 .Detach(IExceptionHandler.DangerouslyIgnoreAllException);
    //         }
    //     }
    //
    //     private async Task ReadAsync(uint pid, NamedPipeServerStream stream, CancellationToken cancellationToken)
    //     {
    //         try
    //         {
    //             if (!clientStreams.TryAdd(pid, stream)) throw new InvalidOperationException($"[{pid}] Client already connected.");
    //             var buffer = new byte[4096];
    //             while (!cancellationToken.IsCancellationRequested)
    //             {
    //                 var bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    //                 if (bytesRead == 0) break;
    //
    //                 var message = ClientMessage.Parser.ParseFrom(buffer.AsSpan(0, bytesRead));
    //                 switch (message.DataCase)
    //                 {
    //                     case ClientMessage.DataOneofCase.Initialized:
    //                     {
    //                         Debug.WriteLine($"[{pid}] Client Initialized: {message.Initialized}");
    //                         break;
    //                     }
    //                     case ClientMessage.DataOneofCase.FocusChanged:
    //                     {
    //                         Debug.WriteLine($"[{pid}] Client FocusChanged: {message.FocusChanged}");
    //                         break;
    //                     }
    //                     case ClientMessage.DataOneofCase.FocusText:
    //                     {
    //                         Debug.WriteLine($"[{pid}] Client FocusText: {message.FocusText}");
    //                         break;
    //                     }
    //                     case ClientMessage.DataOneofCase.EndEdit:
    //                     {
    //                         Debug.WriteLine($"[{pid}] Client EndEdit: {message.EndEdit}");
    //                         break;
    //                     }
    //                 }
    //             }
    //         }
    //         finally
    //         {
    //             Debug.WriteLine($"[{pid}] Client disconnected");
    //             clientStreams.TryRemove(pid, out _);
    //             await stream.DisposeAsync();
    //         }
    //     }
    //
    //     private unsafe static uint GetProcessId(HWND hWnd)
    //     {
    //         uint pid = 0;
    //         return PInvoke.GetWindowThreadProcessId(hWnd, &pid) == 0 ? 0 : pid;
    //     }
    //
    //     private unsafe static Task ActivateTextServiceOnTargetWindow(HWND hWnd)
    //     {
    //         var taskCompletionSource = new TaskCompletionSource();
    //
    //         new Thread(() =>
    //         {
    //             while (!PInvoke.IsWindow(hWnd))
    //             {
    //                 hWnd = PInvoke.GetParent(hWnd);
    //                 if (hWnd == HWND.Null)
    //                 {
    //                     taskCompletionSource.SetException(new InvalidOperationException("Failed to find a valid window handle."));
    //                     return;
    //                 }
    //             }
    //
    //             var tid = PInvoke.GetCurrentThreadId();
    //             var targetTid = PInvoke.GetWindowThreadProcessId(hWnd, null);
    //             if (!PInvoke.AttachThreadInput(tid, targetTid, true))
    //             {
    //                 taskCompletionSource.SetException(Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error())!);
    //                 return;
    //             }
    //
    //             try
    //             {
    //                 var previousHkl = PInvoke.GetKeyboardLayout(targetTid);
    //                 var hkl = PInvoke.LoadKeyboardLayout("11450409", ACTIVATE_KEYBOARD_LAYOUT_FLAGS.KLF_ACTIVATE);
    //                 PInvoke.PostMessage(
    //                     hWnd,
    //                     0x0050, // WM_INPUTLANGCHANGEREQUEST
    //                     1,
    //                     hkl.DangerousGetHandle());
    //                 PInvoke.PostMessage(
    //                     hWnd,
    //                     0x0050, // WM_INPUTLANGCHANGEREQUEST
    //                     1,
    //                     new IntPtr(previousHkl.Value));
    //             }
    //             finally
    //             {
    //                 PInvoke.AttachThreadInput(tid, targetTid, false);
    //             }
    //         }).With(t => t.SetApartmentState(ApartmentState.STA)).Start();
    //
    //         return taskCompletionSource.Task;
    //     }
    //
    //     public void Dispose()
    //     {
    //         cancellationTokenSource.Cancel();
    //     }
    // }
}

// internal unsafe class DwmScreenCaptureHelper : IDisposable
// {
//     private readonly HWND hostWnd;
//
//     public DwmScreenCaptureHelper()
//     {
//         hostWnd = PInvoke.CreateWindowEx(
//             WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE |
//             WINDOW_EX_STYLE.WS_EX_LAYERED | WINDOW_EX_STYLE.WS_EX_TRANSPARENT,
//             "STATIC",
//             string.Empty,
//             WINDOW_STYLE.WS_POPUP | WINDOW_STYLE.WS_VISIBLE,
//             0,
//             0,
//             0,
//             0,
//             HWND.Null,
//             null,
//             null,
//             null);
//         PInvoke.SetLayeredWindowAttributes(hostWnd, new COLORREF(0), 0, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);
//     }
//
//     ~DwmScreenCaptureHelper()
//     {
//         if (!hostWnd.IsNull) PInvoke.DestroyWindow(hostWnd);
//     }
//
//     // https://blog.adeltax.com/dwm-thumbnails-but-with-idcompositionvisual/
//     // https://gist.github.com/ADeltaX/aea6aac248604d0cb7d423a61b06e247
//     public Bitmap Capture(nint hWnd)
//     {
//         DwmpQueryWindowThumbnailSourceSize((HWND)hWnd, false, out var srcSize).ThrowOnFailure();
//         if (srcSize.Width == 0 || srcSize.Height == 0)
//             throw new InvalidOperationException("Failed to query thumbnail source size.");
//
//         using var device = D3D11.D3D11CreateDevice(DriverType.Hardware, DeviceCreationFlags.BgraSupport);
//         using var dxgiDevice = device.QueryInterface<IDXGIDevice>();
//         var iid = typeof(IDCompositionDesktopDevice).GUID;
//         DCompositionCreateDevice3(
//             dxgiDevice.NativePointer,
//             ref iid,
//             out var pDCompositionDesktopDevice).ThrowOnFailure();
//         using var dCompositionDesktopDevice = new IDCompositionDesktopDevice(pDCompositionDesktopDevice);
//
//         var thumbProperties = new DWM_THUMBNAIL_PROPERTIES
//         {
//             dwFlags = 0x00000001 | 0x00000002 | 0x00000004 | 0x00000008 | 0x00000010,
//             fVisible = true,
//             fSourceClientAreaOnly = false,
//             opacity = 255,
//             rcDestination = new RECT(0, 0, srcSize.Width, srcSize.Height),
//             rcSource = new RECT(0, 0, srcSize.Width, srcSize.Height),
//         };
//         DwmpCreateSharedThumbnailVisual(
//             hostWnd,
//             (HWND)hWnd,
//             2,
//             ref thumbProperties,
//             pDCompositionDesktopDevice,
//             out var pDCompositionVisual,
//             out var thumb).ThrowOnFailure();
//         using var dCompositionVisual = new IDCompositionVisual2(pDCompositionVisual);
//
//         // using var dxgiAdapter = dxgiDevice.GetAdapter();
//         // using var dxgiFactory = dxgiAdapter.GetParent<IDXGIFactory2>();
//         // using var swapChain = dxgiFactory.CreateSwapChainForComposition(
//         //     device,
//         //     new SwapChainDescription1
//         //     {
//         //         Width = (uint)srcSize.Width,
//         //         Height = (uint)srcSize.Height,
//         //         Format = Format.B8G8R8A8_UNorm,
//         //         Stereo = false,
//         //         SampleDescription = new SampleDescription(1, 0),
//         //         BufferUsage = Usage.RenderTargetOutput,
//         //         BufferCount = 1,
//         //         Scaling = Scaling.Stretch,
//         //         SwapEffect = SwapEffect.Discard,
//         //         AlphaMode = AlphaMode.Premultiplied,
//         //         Flags = SwapChainFlags.None
//         //     });
//         // using var buffer = swapChain.GetBuffer<ID3D11Texture2D>(0);
//         // using var resource = new IDXGIResource(buffer.NativePointer);
//         //
//         // using var dCompositionSurface = dCompositionDesktopDevice.CreateSurfaceFromHandle(resource.SharedHandle);
//
//         dCompositionDesktopDevice.CreateTargetForHwnd(
//             hostWnd,
//             false,
//             out var dCompositionTarget).CheckError();
//         dCompositionTarget.SetRoot(dCompositionVisual).CheckError();
//
//         // using var dCompositionSurface = dCompositionDesktopDevice.CreateSurface(
//         //     (uint)srcSize.Width,
//         //     (uint)srcSize.Height,
//         //     Format.B8G8R8A8_UNorm,
//         //     AlphaMode.Premultiplied);
//         // dCompositionVisual.SetContent(dCompositionSurface).CheckError(); // <- ERROR
//
//         dCompositionDesktopDevice.Commit().CheckError();
//         dCompositionDesktopDevice.WaitForCommitCompletion().CheckError();
//
//         // dCompositionSurface.BeginDraw<IDXGISurface>(null, out var dxgiSurface, out _).CheckError();
//         // if (dxgiSurface == null) throw new InvalidOperationException("Failed to begin draw.");
// //
//         // try
//         // {
//         //     var data = dxgiSurface.Map(Vortice.DXGI.MapFlags.Read);
//         //     return new Bitmap(
//         //         PixelFormat.Bgra8888,
//         //         AlphaFormat.Premul,
//         //         data.DataPointer,
//         //         new PixelSize(srcSize.Width, srcSize.Height),
//         //         new Vector(96d, 96d),
//         //         (int)data.Pitch
//         //     );
//         // }
//         // finally
//         // {
//         //     dxgiSurface.Unmap();
//         //     dCompositionSurface.EndDraw().CheckError();
//         // }
//
//         throw new NotImplementedException();
//     }
//
//     public void Dispose()
//     {
//         if (!hostWnd.IsNull) PInvoke.DestroyWindow(hostWnd);
//         GC.SuppressFinalize(this);
//     }
//
//     [DllImport("dcomp.dll", ExactSpelling = true), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
//     private static extern HRESULT DCompositionCreateDevice3(
//         [In] nint renderingDevice,
//         [In] ref Guid iid,
//         [Out] out nint pDCompositionDesktopDevice);
//
//     [DllImport("dwmapi.dll", CallingConvention = CallingConvention.Winapi, PreserveSig = true, EntryPoint = "#147")]
//     [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
//     private static extern HRESULT DwmpCreateSharedThumbnailVisual(
//         [In] HWND hWndDestination,
//         [In] HWND hWndSource,
//         [In] uint thumbnailFlags,
//         [In] ref DWM_THUMBNAIL_PROPERTIES thumbnailProperties,
//         [In] nint pDCompositionDesktopDevice,
//         [Out] out nint pDCompositionVisual,
//         [Out] out nint hThumbnailId);
//
//     [DllImport("dwmapi.dll", CallingConvention = CallingConvention.Winapi, PreserveSig = true, EntryPoint = "#162")]
//     [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
//     private static extern HRESULT DwmpQueryWindowThumbnailSourceSize(
//         [In] HWND hWndSource,
//         [In] BOOL fSourceClientAreaOnly,
//         [Out] out SIZE pSize);
// }

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