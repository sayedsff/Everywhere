using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Avalonia;
using Everywhere.Enums;
using Everywhere.Extensions;
using Everywhere.Interfaces;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Google.Protobuf;
using Debug = System.Diagnostics.Debug;

namespace Everywhere.Windows.Services;

public class Win32VisualElementContext : IVisualElementContext
{
    private const string PipeName = "everywhere_text_service";

    private static readonly UIA3Automation Automation = new();
    private static readonly ITreeWalker TreeWalker = Automation.TreeWalkerFactory.GetRawViewWalker();
    private static readonly TextServiceImpl TextService = new();

    public IVisualElement? KeyboardFocusedElement => TryFrom(Automation.FocusedElement);

    public IVisualElement? PointerOverElement => TryFrom(
        static () => PInvoke.GetCursorPos(out var point) ? Automation.FromPoint(point) : null);

    private static VisualElementImpl? TryFrom(Func<AutomationElement?> factory)
    {
        try
        {
            if (factory() is { } element) return new VisualElementImpl(element);
        }
        catch (Exception ex)
        {
            // Log the exception if needed
            Console.WriteLine($"Error retrieving UI Automation element: {ex.Message}");
        }

        return null;
    }

    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    private class VisualElementImpl(AutomationElement element) : IVisualElement
    {
        public string Id { get; } = string.Join('.', element.Properties.RuntimeId.ValueOrDefault);

        public IVisualElement? Parent
        {
            get
            {
                var parent = TreeWalker.GetParent(element);
                return parent is null ? null : new VisualElementImpl(parent);
            }
        }

        public IEnumerable<IVisualElement> Children
        {
            get
            {
                var child = TreeWalker.GetFirstChild(element);
                while (child is not null)
                {
                    yield return new VisualElementImpl(child);
                    child = TreeWalker.GetNextSibling(child);
                }
            }
        }

        public VisualElementType Type => element.Properties.ControlType.ValueOrDefault switch
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

        public VisualElementStates States
        {
            get
            {
                var states = VisualElementStates.None;
                if (element.Properties.IsOffscreen.ValueOrDefault) states |= VisualElementStates.Offscreen;
                if (!element.Properties.IsEnabled.ValueOrDefault) states |= VisualElementStates.Disabled;
                if (element.Properties.HasKeyboardFocus.ValueOrDefault) states |= VisualElementStates.Focused;
                if (element.Patterns.SelectionItem.PatternOrDefault is { IsSelected.ValueOrDefault: true }) states |= VisualElementStates.Selected;
                if (element.Patterns.Value.PatternOrDefault is { IsReadOnly.ValueOrDefault: true }) states |= VisualElementStates.ReadOnly;
                if (element.Properties.IsPassword.ValueOrDefault) states |= VisualElementStates.Password;
                return states;
            }
        }

        public string? Name => element.Properties.Name.ValueOrDefault;

        public PixelRect BoundingRectangle => new(
            element.BoundingRectangle.X,
            element.BoundingRectangle.Y,
            element.BoundingRectangle.Width,
            element.BoundingRectangle.Height);

        public uint ProcessId => (uint)element.FrameworkAutomationElement.ProcessId.ValueOrDefault;

        public string? GetText(int maxLength = -1)
        {
            if (element.Patterns.Value.PatternOrDefault is { } valuePattern) return valuePattern.Value;
            if (element.Patterns.Text.PatternOrDefault is { } textPattern) return textPattern.DocumentRange.GetText(maxLength);
            return null;
        }

        public void SetText(string text, bool append)
        {
            if (States.HasFlag(VisualElementStates.Disabled | VisualElementStates.ReadOnly))
            {
                throw new InvalidOperationException("Cannot set text on a disabled or read-only element.");
            }

            element.Focus();

            var pid = element.FrameworkAutomationElement.ProcessId.ValueOrDefault;
            if (TryGetWindow(element, out var hWnd) || TryGetWindow((uint)pid, out hWnd))
            {
                PInvoke.SetForegroundWindow((HWND)hWnd);
            }
            else
            {
                throw new InvalidOperationException("Cannot set text on an element without a valid window handle or process ID.");
            }

            _ = TrySetValueWithValuePattern() ||
                TrySetValueWithTextService() ||
                TrySetValueWithSendInput();

            bool TrySetValueWithValuePattern()
            {
                return false;
                if (element.Patterns.Value.PatternOrDefault is not { } valuePattern) return false;

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

            bool TrySetValueWithTextService()
            {
                TextService.SendAsync(
                    new ServerMessage
                    {
                        SetFocusText = new SetFocusText
                        {
                            Text = text,
                            Append = append
                        }
                    },
                    (uint)pid,
                    (HWND)hWnd); // todo: async

                return true;
            }

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

        private string DebuggerDisplay => $"[{element.ControlType}] {Name} - {GetText()}";

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
    }

    private class TextServiceImpl : IDisposable
    {
        public uint FocusedPid { get; private set; }
        public nint FocusedHWnd { get; private set; }

        private readonly CancellationTokenSource cancellationTokenSource = new();
        private readonly ConcurrentDictionary<uint, NamedPipeServerStream> clientStreams = new();

        public TextServiceImpl()
        {
            Task.Run(() => HostAsync(cancellationTokenSource.Token));
        }

        public async Task<bool> SendAsync(ServerMessage message, uint pid, HWND hWnd, CancellationToken cancellationToken = default)
        {
            if (pid == 0) pid = GetProcessId(hWnd);
            if (pid == 0) return false;

            if (!clientStreams.TryGetValue(pid, out var stream))
            {
                await ActivateTextServiceOnTargetWindow(hWnd);

                for (var i = 0; i < 6; i++)
                {
                    await Task.Delay(500, cancellationToken);
                    if (clientStreams.TryGetValue(pid, out stream)) break;
                }

                if (stream == null) return false; // Failed to connect to the client
            }

            var data = message.ToByteArray();
            await stream.WriteAsync(data, cancellationToken);
            return true;
        }

        private async Task HostAsync(CancellationToken cancellationToken)
        {
            Debug.WriteLine("Starting NamedPipeServerStream...");
            while (!cancellationToken.IsCancellationRequested)
            {
                var stream = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Message,
                    PipeOptions.Asynchronous);
                await stream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                Debug.WriteLine("Client connected, waiting for initialization...");
                uint pid;
                try
                {
                    var buffer = new byte[4096];
                    var bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (bytesRead == 0) throw new InvalidDataException("Client disconnected before sending data.");

                    var message = ClientMessage.Parser.ParseFrom(buffer.AsSpan(0, bytesRead));
                    if (message.DataCase != ClientMessage.DataOneofCase.Initialized)
                    {
                        throw new InvalidDataException("Invalid message received when waiting for initialization.");
                    }

                    pid = message.Initialized.Pid;
                    Debug.WriteLine($"[{pid}] Client Initialized");
                }
                catch
                {
                    await stream.DisposeAsync();
                    throw;
                }

                Task.Run(() => ReadAsync(pid, stream, cancellationToken), cancellationToken)
                    .Detach(IExceptionHandler.DangerouslyIgnoreAllException);
            }
        }

        private async Task ReadAsync(uint pid, NamedPipeServerStream stream, CancellationToken cancellationToken)
        {
            try
            {
                if (!clientStreams.TryAdd(pid, stream)) throw new InvalidOperationException($"[{pid}] Client already connected.");
                var buffer = new byte[4096];
                while (!cancellationToken.IsCancellationRequested)
                {
                    var bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (bytesRead == 0) break;

                    var message = ClientMessage.Parser.ParseFrom(buffer.AsSpan(0, bytesRead));
                    switch (message.DataCase)
                    {
                        case ClientMessage.DataOneofCase.Initialized:
                        {
                            Debug.WriteLine($"[{pid}] Client Initialized: {message.Initialized}");
                            break;
                        }
                        case ClientMessage.DataOneofCase.FocusChanged:
                        {
                            Debug.WriteLine($"[{pid}] Client FocusChanged: {message.FocusChanged}");
                            break;
                        }
                        case ClientMessage.DataOneofCase.FocusText:
                        {
                            Debug.WriteLine($"[{pid}] Client FocusText: {message.FocusText}");
                            break;
                        }
                        case ClientMessage.DataOneofCase.EndEdit:
                        {
                            Debug.WriteLine($"[{pid}] Client EndEdit: {message.EndEdit}");
                            break;
                        }
                    }
                }
            }
            finally
            {
                Debug.WriteLine($"[{pid}] Client disconnected");
                clientStreams.TryRemove(pid, out _);
                await stream.DisposeAsync();
            }
        }

        private unsafe static uint GetProcessId(HWND hWnd)
        {
            uint pid = 0;
            return PInvoke.GetWindowThreadProcessId(hWnd, &pid) == 0 ? 0 : pid;
        }

        private unsafe static Task ActivateTextServiceOnTargetWindow(HWND hWnd)
        {
            var taskCompletionSource = new TaskCompletionSource();

            new Thread(
                () =>
                {
                    while (!PInvoke.IsWindow(hWnd))
                    {
                        hWnd = PInvoke.GetParent(hWnd);
                        if (hWnd == HWND.Null)
                        {
                            taskCompletionSource.SetException(new InvalidOperationException("Failed to find a valid window handle."));
                            return;
                        }
                    }

                    var tid = PInvoke.GetCurrentThreadId();
                    var targetTid = PInvoke.GetWindowThreadProcessId(hWnd, null);
                    if (!PInvoke.AttachThreadInput(tid, targetTid, true))
                    {
                        taskCompletionSource.SetException(Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error())!);
                        return;
                    }

                    try
                    {
                        var previousHkl = PInvoke.GetKeyboardLayout(targetTid);
                        var hkl = PInvoke.LoadKeyboardLayout("11450409", ACTIVATE_KEYBOARD_LAYOUT_FLAGS.KLF_ACTIVATE);
                        PInvoke.PostMessage(
                            hWnd,
                            0x0050, // WM_INPUTLANGCHANGEREQUEST
                            1,
                            hkl.DangerousGetHandle());
                        PInvoke.PostMessage(
                            hWnd,
                            0x0050, // WM_INPUTLANGCHANGEREQUEST
                            1,
                            new IntPtr(&previousHkl));
                    }
                    finally
                    {
                        PInvoke.AttachThreadInput(tid, targetTid, false);
                    }
                }).With(t => t.SetApartmentState(ApartmentState.STA)).Start();

            return taskCompletionSource.Task;
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
        }
    }
}