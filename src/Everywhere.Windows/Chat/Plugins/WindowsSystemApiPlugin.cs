using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Everywhere.Chat.Plugins;
using Everywhere.Common;
using Everywhere.I18N;
using Lucide.Avalonia;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Everywhere.Windows.Chat.Plugins;

/// <summary>
/// Simplified Windows system helper plugin that exposes a compact tool surface area.
/// </summary>
public class WindowsSystemApiPlugin : BuiltInChatPlugin
{
    public override LucideIconKind? Icon => LucideIconKind.Monitor;

    private readonly ILogger<WindowsSystemApiPlugin> _logger;

    public WindowsSystemApiPlugin(ILogger<WindowsSystemApiPlugin> logger) : base("WindowsSystemApi")
    {
        _logger = logger;

        _functions.Add(new AnonymousChatFunction(OpenControlPanelAsync, ChatFunctionPermissions.ProcessAccess));
        _functions.Add(new AnonymousChatFunction(MouseActionAsync, ChatFunctionPermissions.ScreenAccess));
        _functions.Add(new AnonymousChatFunction(KeyboardActionAsync, ChatFunctionPermissions.ScreenAccess));
    }

    private static readonly IReadOnlyDictionary<ControlPanelItem, string> ControlPanelArguments = new Dictionary<ControlPanelItem, string>
    {
        { ControlPanelItem.Home, string.Empty },
        { ControlPanelItem.NetworkConnections, "ncpa.cpl" },
        { ControlPanelItem.PowerOptions, "/name Microsoft.PowerOptions" },
        { ControlPanelItem.ProgramsAndFeatures, "appwiz.cpl" },
        { ControlPanelItem.System, "/name Microsoft.System" },
        { ControlPanelItem.DeviceManager, "hdwwiz.cpl" },
        { ControlPanelItem.Sound, "mmsys.cpl" },
        { ControlPanelItem.Display, "/name Microsoft.Display" },
        { ControlPanelItem.UserAccounts, "/name Microsoft.UserAccounts" },
        { ControlPanelItem.WindowsUpdate, "/name Microsoft.WindowsUpdate" },
        { ControlPanelItem.DateTime, "timedate.cpl" }
    };

    [KernelFunction("open_control_panel")]
    [Description("Launches Control Panel tasks the same way the control.exe command does. Useful for opening specific Windows settings panes.")]
    private Task<string> OpenControlPanelAsync(
        [Description("The Control Panel item to open. Matches control.exe canonical names.")] ControlPanelItem item,
        [Description("Optional override for the control.exe argument when you already know the exact command.")]
        string? argument = null)
    {
        _logger.LogDebug("Launching Control Panel item {Item} with override {Override}", item, argument);

        return Task.Run(() =>
        {
            var args = argument ?? ControlPanelArguments.GetValueOrDefault(item, string.Empty);
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.System),
                        "control.exe"), // full path to avoid possible hijacking
                    Arguments = args,
                    UseShellExecute = true
                };

                Process.Start(psi);
                return "success";
            }
            catch (Exception ex)
            {
                var handledException = new HandledException(ex, new DirectResourceKey("Failed to launch Control Panel item"));
                _logger.LogError(ex, "Failed to launch Control Panel item {Item} with args {Args}", item, args);
                throw handledException;
            }
        });
    }

    [KernelFunction("mouse_action")]
    [Description("Performs mouse actions compatible with the computer-use tool schema (move, click, double-click, scroll).")]
    private Task<string> MouseActionAsync(
        [Description("Type of action to perform.")] MouseActionKind action,
        [Description("Mouse button for click actions.")] MouseButtonKind button = MouseButtonKind.Left,
        [Description("Target X coordinate in screen pixels. Required for Move, optional otherwise.")]
        int? x = null,
        [Description("Target Y coordinate in screen pixels. Required for Move, optional otherwise.")]
        int? y = null,
        [Description("Scroll delta in wheel ticks (positive scrolls up). Only used for Scroll actions.")]
        int scrollDelta = 120)
    {
        _logger.LogDebug("Mouse action {Action} at ({X},{Y}) button {Button}", action, x, y, button);

        return Task.Run(async () =>
        {
            try
            {
                if (x.HasValue && y.HasValue)
                {
                    if (!PInvoke.SetCursorPos(x.Value, y.Value))
                    {
                        throw new InvalidOperationException($"Failed to move cursor to ({x.Value}, {y.Value}).");
                    }
                }

                switch (action)
                {
                    case MouseActionKind.Move:
                    {
                        return "success";
                    }
                    case MouseActionKind.Click:
                    {
                        PerformMouseClick(button);
                        return "success";
                    }
                    case MouseActionKind.DoubleClick:
                    {
                        PerformMouseClick(button);
                        await Task.Delay(TimeSpan.FromSeconds(0.1));
                        PerformMouseClick(button);
                        return "success";
                    }
                    case MouseActionKind.Scroll:
                    {
                        if (scrollDelta == 0)
                        {
                            throw new InvalidOperationException($"Scroll delta cannot be zero.");
                        }

                        var scrollInput = new INPUT
                        {
                            type = INPUT_TYPE.INPUT_MOUSE,
                            Anonymous = new INPUT._Anonymous_e__Union
                            {
                                mi =
                                {
                                    dwFlags = MOUSE_EVENT_FLAGS.MOUSEEVENTF_WHEEL,
                                    mouseData = (uint)scrollDelta
                                }
                            }
                        };
                        PInvoke.SendInput([scrollInput], Marshal.SizeOf<INPUT>());
                        return "success";
                    }
                    default:
                    {
                        throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported mouse action.");
                    }
                }
            }
            catch (Exception ex)
            {
                var handledException = new HandledException(ex, new DirectResourceKey("Failed to run mouse action"));
                _logger.LogError(ex, "Failed to run mouse action {Action}", action);
                throw handledException;
            }
        });
    }

    [KernelFunction("keyboard_action")]
    [Description("Simulates keyboard input compatible with the computer-use tool schema (type text or send common shortcuts).")]
    private Task<string> KeyboardActionAsync(
        [Description("Keyboard action type.")] KeyboardActionKind action,
        [Description("Free-form text to type when action is TypeText.")] string? text = null,
        [Description("Predefined shortcut to send when action is Shortcut.")] KeyboardShortcut? shortcut = null)
    {
        _logger.LogDebug("Keyboard action {Action} with text '{Text}' shortcut {Shortcut}", action, text, shortcut);

        return Task.Run(() =>
        {
            try
            {
                switch (action)
                {
                    case KeyboardActionKind.TypeText:
                    {
                        if (string.IsNullOrEmpty(text))
                        {
                            throw new ArgumentException($"{nameof(text)} must be provided for {nameof(KeyboardActionKind.TypeText)}.");
                        }

                        SendKeys.SendWait(text);
                        return "success";
                    }
                    case KeyboardActionKind.Shortcut:
                    {
                        if (shortcut is null)
                        {
                            throw new ArgumentException($"{nameof(shortcut)} must be provided for {nameof(KeyboardActionKind.Shortcut)}.");
                        }

                        SendKeys.SendWait(GetShortcutSequence(shortcut.Value));
                        return "success";
                    }
                    default:
                    {
                        throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported keyboard action.");
                    }
                }
            }
            catch (Exception ex)
            {
                var handledException = new HandledException(ex, new DirectResourceKey("Failed to run keyboard action"));
                _logger.LogError(ex, "Failed to run keyboard action {Action}", action);
                throw handledException;
            }
        });
    }

    private static void PerformMouseClick(MouseButtonKind button)
    {
        var (down, up) = button switch
        {
            MouseButtonKind.Left => (MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN, MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP),
            MouseButtonKind.Right => (MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTDOWN, MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTUP),
            MouseButtonKind.Middle => (MOUSE_EVENT_FLAGS.MOUSEEVENTF_MIDDLEDOWN, MOUSE_EVENT_FLAGS.MOUSEEVENTF_MIDDLEUP),
            MouseButtonKind.XButton1 => (MOUSE_EVENT_FLAGS.MOUSEEVENTF_XDOWN, MOUSE_EVENT_FLAGS.MOUSEEVENTF_XUP),
            MouseButtonKind.XButton2 => (MOUSE_EVENT_FLAGS.MOUSEEVENTF_XDOWN, MOUSE_EVENT_FLAGS.MOUSEEVENTF_XUP),
            _ => (MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN, MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP)
        };
        nuint dwExtraInfo = button switch
        {
            MouseButtonKind.XButton1 => 1,
            MouseButtonKind.XButton2 => 2,
            _ => 0
        };

        var inputs = (Span<INPUT>)stackalloc INPUT[2];
        inputs[0] = new INPUT
        {
            type = INPUT_TYPE.INPUT_MOUSE,
            Anonymous =
            {
                mi = new MOUSEINPUT
                {
                    dwFlags = down,
                    dwExtraInfo = dwExtraInfo
                }
            }
        };
        inputs[1] = new INPUT
        {
            type = INPUT_TYPE.INPUT_MOUSE,
            Anonymous =
            {
                mi = new MOUSEINPUT
                {
                    dwFlags = up,
                    dwExtraInfo = dwExtraInfo
                }
            }
        };

        PInvoke.SendInput(inputs, Marshal.SizeOf<INPUT>());
    }

    private static string GetShortcutSequence(KeyboardShortcut shortcut) => shortcut switch
    {
        KeyboardShortcut.Copy => "^c",
        KeyboardShortcut.Paste => "^v",
        KeyboardShortcut.Cut => "^x",
        KeyboardShortcut.SelectAll => "^a",
        KeyboardShortcut.Undo => "^z",
        KeyboardShortcut.Redo => "^y",
        KeyboardShortcut.Save => "^s",
        KeyboardShortcut.Open => "^o",
        KeyboardShortcut.Find => "^f",
        KeyboardShortcut.New => "^n",
        KeyboardShortcut.Close => "^w",
        _ => throw new ArgumentOutOfRangeException(nameof(shortcut), shortcut, "Shortcut not supported.")
    };

    public enum ControlPanelItem
    {
        Home,
        NetworkConnections,
        PowerOptions,
        ProgramsAndFeatures,
        System,
        DeviceManager,
        Sound,
        Display,
        UserAccounts,
        WindowsUpdate,
        DateTime
    }

    public enum MouseActionKind
    {
        Move,
        Click,
        DoubleClick,
        Scroll
    }

    public enum MouseButtonKind
    {
        Left,
        Right,
        Middle,
        XButton1,
        XButton2
    }

    public enum KeyboardActionKind
    {
        TypeText,
        Shortcut
    }

    public enum KeyboardShortcut
    {
        Copy,
        Paste,
        Cut,
        SelectAll,
        Undo,
        Redo,
        Save,
        Open,
        Find,
        New,
        Close
    }
}