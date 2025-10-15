using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Everywhere.Chat.Plugins;
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

        _functions.Add(new AnonymousChatFunction(UseControlPanelAsync, ChatFunctionPermissions.ScreenAccess));
        _functions.Add(new AnonymousChatFunction(SetSystemSettingsNumberAsync, ChatFunctionPermissions.ScreenAccess));
        _functions.Add(new AnonymousChatFunction(SetSystemSettingsBoolAsync, ChatFunctionPermissions.ScreenAccess));
        _functions.Add(new AnonymousChatFunction(ComputerMouseActionAsync, ChatFunctionPermissions.ScreenAccess));
        _functions.Add(new AnonymousChatFunction(ComputerKeyboardActionAsync, ChatFunctionPermissions.ScreenAccess));
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

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, int dx, int dy, int dwData, UIntPtr dwExtraInfo);

    [KernelFunction("use_control_panel")]
    [Description("Launches Control Panel tasks the same way the control.exe command does. Useful for opening specific Windows settings panes.")]
    private Task<string> UseControlPanelAsync(
        [Description("The Control Panel item to open. Matches control.exe canonical names.")] ControlPanelItem item,
        [Description("Optional override for the control.exe argument when you already know the exact command.")] string? argument = null)
    {
        _logger.LogDebug("Launching Control Panel item {Item} with override {Override}", item, argument);

        return Task.Run(() =>
        {
            var args = argument ?? ControlPanelArguments.GetValueOrDefault(item, string.Empty);
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "control.exe",
                    Arguments = args,
                    UseShellExecute = true
                };

                Process.Start(psi);
                return string.IsNullOrWhiteSpace(args)
                    ? $"Opened Control Panel ({item})."
                    : $"Executed control.exe {args} for {item}.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open Control Panel item {Item}", item);
                throw new InvalidOperationException($"Failed to open {item}: {ex.Message}");
            }
        });
    }

    [KernelFunction("set_system_settings_number")]
    [Description("Adjusts numeric Windows system settings such as volume or display brightness. Values map 0-100 inclusive.")]
    private async Task<string> SetSystemSettingsNumberAsync(
        [Description("Setting to change. Use the enum values so the assistant knows what is supported.")] SystemSettingNumberTarget target,
        [Description("Desired value in the range 0-100.")] int value)
    {
        _logger.LogDebug("Changing numeric setting {Target} => {Value}", target, value);

        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Value must be between 0 and 100.");
        }

        return await Task.Run(() =>
        {
            try
            {
                switch (target)
                {
                    case SystemSettingNumberTarget.Volume:
                        AudioControl.SetMasterVolume(value);
                        return $"System volume set to {value}%.";

                    case SystemSettingNumberTarget.Brightness:
                        RunPowerShellOrThrow($"$brightness = {value}\n(Get-WmiObject -Namespace root/WMI -Class WmiMonitorBrightnessMethods).WmiSetBrightness(1, $brightness)");
                        return value == 0 ? "Display brightness set to minimum." : $"Display brightness set to {value}%";

                    default:
                        throw new InvalidOperationException($"Setting {target} is not supported.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to change numeric system setting {Target}", target);
                throw new InvalidOperationException($"Unable to update {target}: {ex.Message}");
            }
        });
    }

    [KernelFunction("set_system_settings_bool")]
    [Description("Turns specific Windows features on or off (boolean toggles). Useful when the assistant needs a binary switch.")]
    private async Task<string> SetSystemSettingsBoolAsync(
        [Description("The toggle target. Values are enumerated to avoid magic strings.")] SystemSettingToggleTarget target,
        [Description("Desired state.")] bool value)
    {
        _logger.LogDebug("Changing boolean setting {Target} => {Value}", target, value);

        return await Task.Run(() =>
        {
            try
            {
                switch (target)
                {
                    case SystemSettingToggleTarget.SystemMute:
                        AudioControl.SetMute(value);
                        return value ? "System audio muted." : "System audio unmuted.";

                    default:
                        throw new InvalidOperationException($"Toggle {target} is not implemented.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to change boolean system setting {Target}", target);
                throw new InvalidOperationException($"Unable to update {target}: {ex.Message}");
            }
        });
    }

    [KernelFunction("computer_mouse_action")]
    [Description("Performs mouse actions compatible with the computer-use tool schema (move, click, double-click, scroll).")]
    private async Task<string> ComputerMouseActionAsync(
        [Description("Type of action to perform.")] MouseActionKind action,
        [Description("Mouse button for click actions.")] MouseButtonKind button = MouseButtonKind.Left,
        [Description("Target X coordinate in screen pixels. Required for Move, optional otherwise.")] int? x = null,
        [Description("Target Y coordinate in screen pixels. Required for Move, optional otherwise.")] int? y = null,
        [Description("Scroll delta in wheel ticks (positive scrolls up). Only used for Scroll actions.")] int scrollDelta = 120)
    {
        _logger.LogDebug("Mouse action {Action} at ({X},{Y}) button {Button}", action, x, y, button);

        return await Task.Run(() =>
        {
            switch (action)
            {
                case MouseActionKind.Move:
                    if (!x.HasValue || !y.HasValue)
                    {
                        throw new ArgumentException("Move requires both X and Y coordinates.");
                    }

                    if (!SetCursorPos(x.Value, y.Value))
                    {
                        throw new InvalidOperationException($"Failed to move cursor to ({x.Value}, {y.Value}).");
                    }

                    return $"Cursor moved to ({x.Value}, {y.Value}).";

                case MouseActionKind.Click:
                    PerformMouseClick(button);
                    return $"Performed {button} click.";

                case MouseActionKind.DoubleClick:
                    PerformMouseClick(button);
                    Thread.Sleep(50);
                    PerformMouseClick(button);
                    return $"Performed {button} double-click.";

                case MouseActionKind.Scroll:
                    if (scrollDelta == 0)
                    {
                        throw new ArgumentException("Scroll delta cannot be zero.");
                    }

                    mouse_event(MOUSEEVENTF_WHEEL, 0, 0, scrollDelta, UIntPtr.Zero);
                    return $"Scrolled {(scrollDelta > 0 ? "up" : "down")} by {Math.Abs(scrollDelta)} ticks.";

                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported mouse action.");
            }
        });
    }

    [KernelFunction("computer_keyboard_action")]
    [Description("Simulates keyboard input compatible with the computer-use tool schema (type text or send common shortcuts).")]
    private async Task<string> ComputerKeyboardActionAsync(
        [Description("Keyboard action type.")] KeyboardActionKind action,
        [Description("Free-form text to type when action is TypeText.")] string? text = null,
        [Description("Predefined shortcut to send when action is Shortcut.")] KeyboardShortcut? shortcut = null)
    {
        _logger.LogDebug("Keyboard action {Action} with text '{Text}' shortcut {Shortcut}", action, text, shortcut);

        return await Task.Run(() =>
        {
            try
            {
                switch (action)
                {
                    case KeyboardActionKind.TypeText:
                        if (string.IsNullOrEmpty(text))
                        {
                            throw new ArgumentException("Text must be provided for TypeText actions.");
                        }

                        SendKeys.SendWait(text);
                        return $"Typed {text.Length} characters.";

                    case KeyboardActionKind.Shortcut:
                        if (shortcut is null)
                        {
                            throw new ArgumentException("Shortcut must be provided when action is Shortcut.");
                        }

                        SendKeys.SendWait(GetShortcutSequence(shortcut.Value));
                        return $"Sent shortcut {shortcut.Value}.";

                    default:
                        throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported keyboard action.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to run keyboard action {Action}", action);
                throw new InvalidOperationException($"Keyboard action failed: {ex.Message}");
            }
        });
    }

    private static void PerformMouseClick(MouseButtonKind button)
    {
        (uint down, uint up) = button switch
        {
            MouseButtonKind.Left => (MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP),
            MouseButtonKind.Right => (MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP),
            MouseButtonKind.Middle => (MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP),
            _ => (MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP)
        };

        mouse_event(down, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(10);
        mouse_event(up, 0, 0, 0, UIntPtr.Zero);
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

    private static void RunPowerShellOrThrow(string script)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start PowerShell.");

        if (!process.WaitForExit(5000))
        {
            try
            {
                process.Kill(true);
            }
            catch
            {
                // ignored - best effort cleanup
            }

            throw new TimeoutException("PowerShell command timed out.");
        }

        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            var message = string.IsNullOrWhiteSpace(error)
                ? $"PowerShell exited with code {process.ExitCode}."
                : error.Trim();
            throw new InvalidOperationException(message);
        }
    }

    private static class AudioControl
    {
    // IID_IAudioEndpointVolume: Interface ID for IAudioEndpointVolume (Windows Core Audio API)
    // Source: https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nn-endpointvolume-iaudioendpointvolume
    // Value: 5CDF2C82-841E-4546-9722-0CF74078229A
    private static readonly Guid AudioEndpointVolumeGuid = new("5CDF2C82-841E-4546-9722-0CF74078229A");
        private static readonly Guid AudioDeviceEnumeratorClassId = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
        private const int CLSCTX_ALL = 23;

        public static void SetMasterVolume(int value)
        {
            Execute(endpoint =>
            {
                var hr = endpoint.SetMasterVolumeLevelScalar(value / 100f, Guid.Empty);
                Marshal.ThrowExceptionForHR(hr);
            });
        }

        public static void SetMute(bool mute)
        {
            Execute(endpoint =>
            {
                var hr = endpoint.SetMute(mute, Guid.Empty);
                Marshal.ThrowExceptionForHR(hr);
            });
        }

        private static void Execute(Action<IAudioEndpointVolume> callback)
        {
            var enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(AudioDeviceEnumeratorClassId, true)!)!;
            Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(0, 1, out var devicePtr));
            var device = (IMMDevice)Marshal.GetObjectForIUnknown(devicePtr);

            try
            {
                var iid = AudioEndpointVolumeGuid;
                Marshal.ThrowExceptionForHR(device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out var endpointPtr));
                var endpoint = (IAudioEndpointVolume)Marshal.GetObjectForIUnknown(endpointPtr);

                try
                {
                    callback(endpoint);
                }
                finally
                {
                    Marshal.ReleaseComObject(endpoint);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(device);
                Marshal.ReleaseComObject(enumerator);
            }
        }
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr pNotify);
        int UnregisterControlChangeNotify(IntPtr pNotify);
        int GetChannelCount(out uint channelCount);
        int SetMasterVolumeLevel(float levelDb, Guid eventContext);
        int SetMasterVolumeLevelScalar(float level, Guid eventContext);
        int GetMasterVolumeLevel(out float levelDb);
        int GetMasterVolumeLevelScalar(out float level);
        int SetChannelVolumeLevel(uint channel, float levelDb, Guid eventContext);
        int SetChannelVolumeLevelScalar(uint channel, float level, Guid eventContext);
        int GetChannelVolumeLevel(uint channel, out float levelDb);
        int GetChannelVolumeLevelScalar(uint channel, out float level);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool isMuted, Guid eventContext);
        int GetMute(out bool isMuted);
        int GetVolumeStepInfo(out uint step, out uint stepCount);
        int VolumeStepUp(Guid eventContext);
        int VolumeStepDown(Guid eventContext);
        int QueryHardwareSupport(out uint hardwareSupportMask);
        int GetVolumeRange(out float volumeMin, out float volumeMax, out float volumeStep);
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr devices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IntPtr endpoint);
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IntPtr device);
        int RegisterEndpointNotificationCallback(IntPtr notify);
        int UnregisterEndpointNotificationCallback(IntPtr notify);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, out IntPtr interfacePointer);
        int OpenPropertyStore(int accessMode, out IntPtr properties);
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        int GetState(out int state);
    }

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

    public enum SystemSettingNumberTarget
    {
        Volume,
        Brightness
    }

    public enum SystemSettingToggleTarget
    {
        SystemMute
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
        Middle
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
