using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Avalonia;
using Everywhere.Enums;
using Everywhere.Extensions;
using Everywhere.Interfaces;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Patterns.Infrastructure;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using WinRT;

namespace Everywhere.Windows.Services;

public class Win32VisualElementContext : IVisualElementContext
{
    private const string PipeName = "everywhere_text_service";

    private static readonly UIA3Automation Automation = new();
    private static readonly ITreeWalker TreeWalker = Automation.TreeWalkerFactory.GetRawViewWalker();
    // private static readonly TextServiceImpl TextService = new();
    private static readonly int CurrentProcessId = (int)PInvoke.GetCurrentProcessId();
    // private static readonly Lazy<DwmScreenCaptureHelper> DwmScreenCaptureHelper = new();
    private static readonly Lazy<Direct3D11ScreenCaptureHelper> Direct3D11ScreenCaptureHelper = new();

    public event IVisualElementContext.KeyboardFocusedElementChangedHandler? KeyboardFocusedElementChanged;

    public IVisualElement? KeyboardFocusedElement => TryFrom(Automation.FocusedElement);

    public IVisualElement? PointerOverElement => TryFrom(static () => PInvoke.GetCursorPos(out var point) ? Automation.FromPoint(point) : null);

    public Win32VisualElementContext()
    {
        Automation.RegisterFocusChangedEvent(
            element =>
            {
                if (KeyboardFocusedElementChanged is not { } handler) return;
                var pid = element?.FrameworkAutomationElement.ProcessId.ValueOrDefault ?? 0;
                if (pid == CurrentProcessId) return;
                handler(element == null ? null : new VisualElementImpl(element));
            });
    }

    private static VisualElementImpl? TryFrom(Func<AutomationElement?> factory)
    {
        try
        {
            if (factory() is { } element && element.FrameworkAutomationElement.ProcessId.ValueOrDefault != CurrentProcessId)
                return new VisualElementImpl(element);
        }
        catch (Exception ex)
        {
            // Log the exception if needed
            Console.WriteLine($"Error retrieving UI Automation element: {ex.Message}");
        }

        return null;
    }

    private class VisualElementImpl(AutomationElement element) : IVisualElement
    {
        private const int CONNECT_E_ADVISELIMIT = unchecked((int)0x80040201);

        public string Id { get; } = string.Join('.', element.Properties.RuntimeId.ValueOrDefault ?? []);

        public IVisualElement? Parent
        {
            get
            {
                try
                {
                    var parent = TreeWalker.GetParent(element);
                    return parent is null ? null : new VisualElementImpl(parent);
                }
                catch (COMException ex) when (ex.HResult == CONNECT_E_ADVISELIMIT)
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
                catch (COMException ex) when (ex.HResult == CONNECT_E_ADVISELIMIT)
                {
                    yield break;
                }
                while (child is not null)
                {
                    yield return new VisualElementImpl(child);
                    try
                    {
                        child = TreeWalker.GetNextSibling(child);
                    }
                    catch (COMException ex) when (ex.HResult == CONNECT_E_ADVISELIMIT)
                    {
                        yield break;
                    }
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
                        ControlType.Document => VisualElementType.TextEdit,
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
                catch (COMException ex) when (ex.HResult == CONNECT_E_ADVISELIMIT)
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
                    if (element.Patterns.SelectionItem.TryGetPattern() is { IsSelected.ValueOrDefault: true }) states |= VisualElementStates.Selected;
                    if (element.Patterns.Value.TryGetPattern() is { IsReadOnly.ValueOrDefault: true }) states |= VisualElementStates.ReadOnly;
                    if (element.Properties.IsPassword.ValueOrDefault) states |= VisualElementStates.Password;
                    return states;
                }
                catch (COMException ex) when (ex.HResult == CONNECT_E_ADVISELIMIT)
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
                    return element.BoundingRectangle.To(
                        r => new PixelRect(
                            r.X,
                            r.Y,
                            r.Width,
                            r.Height));
                }
                catch (COMException ex) when (ex.HResult == CONNECT_E_ADVISELIMIT)
                {
                    return default;
                }
            }
        }

        public int ProcessId
        {
            get
            {
                try
                {
                    return element.FrameworkAutomationElement.ProcessId.ValueOrDefault;
                }
                catch (COMException ex) when (ex.HResult == CONNECT_E_ADVISELIMIT)
                {
                    return 0;
                }
            }
        }

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
            catch (COMException ex) when (ex.HResult == CONNECT_E_ADVISELIMIT) { }

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

            if (!TryGetWindow(element, out var hWnd))
                throw new InvalidOperationException("Cannot capture an element without a valid window handle.");

            return Direct3D11ScreenCaptureHelper.Value.CaptureAsync(hWnd);

            // try
            // {
            //     return Task.FromResult(DwmScreenCaptureHelper.Value.Capture(hWnd));
            // }
            // catch
            // {
            //     return Direct3D11ScreenCaptureHelper.Value.CaptureAsync(hWnd);
            // }
        }

        #region Events

        public event Action<IVisualElement>? TextChanged;

        public event Action<IVisualElement>? BoundingRectangleChanged;

        private Action<IVisualElement>? textChanged;
        private Action<IVisualElement>? boundingRectangleChanged;

        private void ConfigurationEvents()
        {
            // element.FrameworkAutomationElement.RegisterPropertyChangedEvent(
            //     TreeScope.Element,
            //     HandlePropertyChanged,
            //     element.FrameworkAutomationElement.PropertyIdLibrary.BoundingRectangle);
            //
            // void HandlePropertyChanged(AutomationElement e, PropertyId propertyId, object value)
            // {
            //     throw new NotImplementedException();
            // }
        }

        #endregion

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

        #endregion

        public override bool Equals(object? obj)
        {
            if (obj is not VisualElementImpl other) return false;
            return Id == other.Id;
        }

        public override int GetHashCode() => Id.GetHashCode();

        public override string ToString() => $"[{element.ControlType}] {Name} - {GetText(128)}";
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
//     public Bitmap Capture(nint hWnd)
//     {
//         PInvoke.GetWindowRect((HWND)hWnd, out var rect);
//         if (rect.Width <= 0 || rect.Height <= 0)
//             throw new InvalidOperationException("Cannot capture a window with zero width or height.");
//
//         PInvoke.DwmRegisterThumbnail(hostWnd, (HWND)hWnd, out var thumb).ThrowOnFailure();
//         PInvoke.DwmQueryThumbnailSourceSize(thumb, out var srcSize);
//         if (srcSize.Width == 0 || srcSize.Height == 0)
//             throw new InvalidOperationException("Failed to query thumbnail source size.");
//
//         PInvoke.SetWindowPos(
//             hostWnd,
//             HWND.Null,
//             0,
//             0,
//             srcSize.Width,
//             srcSize.Height,
//             SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
//         PInvoke.ShowWindow(hostWnd, SHOW_WINDOW_CMD.SW_SHOW);
//
//         PInvoke.DwmUpdateThumbnailProperties(
//             thumb,
//             new DWM_THUMBNAIL_PROPERTIES
//             {
//                 dwFlags = 0x00000001 | 0x00000008 | 0x00000010,
//                 rcDestination = new RECT(0, 0, srcSize.Width, srcSize.Height),
//                 fVisible = true,
//                 fSourceClientAreaOnly = false,
//             }).ThrowOnFailure();
//
//         var bitmap = new Bitmap(srcSize.Width, srcSize.Height, PixelFormat.Format32bppArgb);
//         using var graphics = Graphics.FromImage(bitmap);
//         var hdcDest = graphics.GetHdc();
//         var hdcSrc = PInvoke.GetDC(hostWnd);
//         PInvoke.BitBlt((HDC)hdcDest, 0, 0, srcSize.Width, srcSize.Height, hdcSrc, 0, 0, ROP_CODE.SRCCOPY);
//         _ = PInvoke.ReleaseDC(hostWnd, hdcSrc);
//         graphics.ReleaseHdc(hdcDest);
//
//         PInvoke.DwmUnregisterThumbnail(thumb);
//         PInvoke.ShowWindow(hostWnd, SHOW_WINDOW_CMD.SW_HIDE);
//
//         return bitmap;
//     }
//
//     public void Dispose()
//     {
//         if (!hostWnd.IsNull) PInvoke.DestroyWindow(hostWnd);
//         GC.SuppressFinalize(this);
//     }
// }

internal class Direct3D11ScreenCaptureHelper
{
    private readonly StrategyBasedComWrappers comWrappers = new();
    private readonly IGraphicsCaptureItemInterop interop;

    public Direct3D11ScreenCaptureHelper()
    {
        var factory = ActivationFactory.Get("Windows.Graphics.Capture.GraphicsCaptureItem");
        Marshal.QueryInterface(factory.ThisPtr, typeof(IGraphicsCaptureItemInterop).GUID, out var pInterop);
        interop = (IGraphicsCaptureItemInterop)comWrappers.GetOrCreateObjectForComInstance(pInterop, CreateObjectFlags.None);
    }

    public async Task<Bitmap> CaptureAsync(nint hWnd)
    {
        using var device = D3D11.D3D11CreateDevice(DriverType.Hardware, DeviceCreationFlags.BgraSupport);
        using var dxgiDevice = device.QueryInterface<IDXGIDevice>();
        CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out var pD3d11Device).ThrowOnFailure();
        using var direct3dDevice = MarshalInterface<IDirect3DDevice>.FromAbi(pD3d11Device);

        var pItem = interop.CreateForWindow(hWnd, new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760"));
        var item = GraphicsCaptureItem.FromAbi(pItem);
        var size = item.Size;

        using var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            direct3dDevice,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            1,
            size);
        var tcs = new TaskCompletionSource<Bitmap>();
        framePool.FrameArrived += (f, _) => tcs.TrySetResult(ToBitmap(f.TryGetNextFrame()));

        using var session = framePool.CreateCaptureSession(item);
        session.IsCursorCaptureEnabled = false;
        session.StartCapture();
        return await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(500));
    }

    private static Bitmap ToBitmap(Direct3D11CaptureFrame frame)
    {
        using var capturedTexture = CreateTexture2D(frame.Surface);

        var device = capturedTexture.Device;
        var description = capturedTexture.Description;
        description.CPUAccessFlags = CpuAccessFlags.Read;
        description.BindFlags = BindFlags.None;
        description.Usage = ResourceUsage.Staging;
        description.MiscFlags = ResourceOptionFlags.None;

        var stagingTexture = device.CreateTexture2D(description);
        device.ImmediateContext.CopyResource(stagingTexture, capturedTexture);

        var mappedSource = device.ImmediateContext.Map(stagingTexture, 0);
        var stagingDescription = stagingTexture.Description;
        return new Bitmap(
            (int)stagingDescription.Width,
            (int)stagingDescription.Height,
            (int)mappedSource.RowPitch,
            PixelFormat.Format32bppArgb,
            mappedSource.DataPointer
        );
    }

    private static ID3D11Texture2D CreateTexture2D(IDirect3DSurface surface)
    {
        var access = CastExtensions.As<IDirect3DDxgiInterfaceAccess>(surface);
        var d3dPtr = access.GetInterface(typeof(ID3D11Texture2D).GUID);
        return new ID3D11Texture2D(d3dPtr);
    }

    [DllImport("d3d11.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern HRESULT CreateDirect3D11DeviceFromDXGIDevice(nint dxgiDevice, out nint graphicsDevice);
}

[GeneratedComInterface]
[Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[ComVisible(true)]
internal partial interface IGraphicsCaptureItemInterop
{
    // https://learn.microsoft.com/windows/win32/api/windows.graphics.capture.interop/nf-windows-graphics-capture-interop-igraphicscaptureiteminterop-createforwindow
    nint CreateForWindow(nint window, in Guid iid);

    // https://learn.microsoft.com/windows/win32/api/windows.graphics.capture.interop/nf-windows-graphics-capture-interop-igraphicscaptureiteminterop-createformonitor
    nint CreateForMonitor(nint monitor, in Guid iid);
}

[ComImport]
[Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[ComVisible(true)]
internal interface IDirect3DDxgiInterfaceAccess
{
    IntPtr GetInterface([In] ref Guid iid);
};

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