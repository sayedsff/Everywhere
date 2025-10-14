using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Avalonia.Input.Platform;
using Everywhere.Chat.Plugins;
using Lucide.Avalonia;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.Win32;

namespace Everywhere.Windows.Chat.Plugins;

public class WindowsSystemApiPlugin : BuiltInChatPlugin
{
    public override LucideIconKind? Icon => LucideIconKind.Monitor;

    private readonly ILogger<WindowsSystemApiPlugin> _logger;
    private readonly IClipboard _clipboard;

    public WindowsSystemApiPlugin(ILogger<WindowsSystemApiPlugin> logger, IClipboard clipboard) : base("WindowsSystemApi")
    {
        _logger = logger;
        _clipboard = clipboard;

        // GUI 交互功能
        _functions.Add(
            new AnonymousChatFunction(
                ShowMessageBoxAsync,
                ChatFunctionPermissions.ScreenAccess));
        
        // 窗口管理功能
        _functions.Add(
            new AnonymousChatFunction(
                ManageWindowAsync,
                ChatFunctionPermissions.ScreenAccess));
        _functions.Add(
            new AnonymousChatFunction(
                GetWindowListAsync,
                ChatFunctionPermissions.ScreenRead));
        
        // 剪贴板功能
        _functions.Add(
            new AnonymousChatFunction(
                GetClipboardTextAsync,
                ChatFunctionPermissions.ClipboardRead));
        _functions.Add(
            new AnonymousChatFunction(
                SetClipboardTextAsync,
                ChatFunctionPermissions.ClipboardAccess));
        
        // 系统设置功能
        _functions.Add(
            new AnonymousChatFunction(
                SetSystemVolumeAsync,
                ChatFunctionPermissions.ScreenAccess));
        _functions.Add(
            new AnonymousChatFunction(
                SetDisplayBrightnessAsync,
                ChatFunctionPermissions.ScreenAccess));
        _functions.Add(
            new AnonymousChatFunction(
                SetDesktopWallpaperAsync,
                ChatFunctionPermissions.ScreenAccess));
        
        // 鼠标和键盘操作
        _functions.Add(
            new AnonymousChatFunction(
                MoveCursorAsync,
                ChatFunctionPermissions.ScreenAccess));
        _functions.Add(
            new AnonymousChatFunction(
                ClickMouseAsync,
                ChatFunctionPermissions.ScreenAccess));
        _functions.Add(
            new AnonymousChatFunction(
                SendKeysAsync,
                ChatFunctionPermissions.ScreenAccess));
        
        // 电源管理
        _functions.Add(
            new AnonymousChatFunction(
                ManagePowerAsync,
                ChatFunctionPermissions.ShellExecute));
        
        // 屏幕控制
        _functions.Add(
            new AnonymousChatFunction(
                TurnOffMonitorAsync,
                ChatFunctionPermissions.ScreenAccess));
    }

    #region Native Methods

    // Window Management
    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    // Mouse and Keyboard
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    // Display and Power
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    // Message Box
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    // Constants
    private const int SW_HIDE = 0;
    private const int SW_SHOWNORMAL = 1;
    private const int SW_SHOWMINIMIZED = 2;
    private const int SW_SHOWMAXIMIZED = 3;
    private const int SW_RESTORE = 9;

    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;

    private const uint KEYEVENTF_KEYUP = 0x0002;

    private const int WM_SYSCOMMAND = 0x0112;
    private const int SC_MONITORPOWER = 0xF170;
    private const int MONITOR_OFF = 2;
    private const int MONITOR_ON = -1;

    private const int SPI_SETDESKWALLPAPER = 0x0014;
    private const int SPIF_UPDATEINIFILE = 0x01;
    private const int SPIF_SENDCHANGE = 0x02;

    private const uint MB_OK = 0x00000000;
    private const uint MB_OKCANCEL = 0x00000001;
    private const uint MB_YESNO = 0x00000004;
    private const uint MB_ICONINFORMATION = 0x00000040;
    private const uint MB_ICONWARNING = 0x00000030;
    private const uint MB_ICONERROR = 0x00000010;

    #endregion

    #region GUI Interaction Functions

    [KernelFunction("show_message_box")]
    [Description("Shows a system message box dialog to the user with customizable buttons and icon.")]
    private async Task<string> ShowMessageBoxAsync(
        [Description("Title of the message box")] string title,
        [Description("Message content to display")] string message,
        [Description("Button type: ok, okcancel, or yesno")] string buttons = "ok",
        [Description("Icon type: info, warning, or error")] string icon = "info")
    {
        _logger.LogDebug("Showing message box: {Title}", title);

        return await Task.Run(() =>
        {
            uint buttonType = buttons.ToLowerInvariant() switch
            {
                "okcancel" => MB_OKCANCEL,
                "yesno" => MB_YESNO,
                _ => MB_OK
            };

            uint iconType = icon.ToLowerInvariant() switch
            {
                "warning" => MB_ICONWARNING,
                "error" => MB_ICONERROR,
                _ => MB_ICONINFORMATION
            };

            int result = MessageBox(IntPtr.Zero, message, title, buttonType | iconType);

            return result switch
            {
                1 => "OK/Yes",
                2 => "Cancel/No",
                _ => "Unknown"
            };
        });
    }

    #endregion

    #region Window Management Functions

    [KernelFunction("get_window_list")]
    [Description("Gets a list of all visible windows with their titles.")]
    private async Task<WindowListResult> GetWindowListAsync()
    {
        _logger.LogDebug("Getting window list");

        return await Task.Run(() =>
        {
            var windows = new List<WindowInfo>();

            EnumWindows((hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd))
                {
                    var sb = new StringBuilder(256);
                    GetWindowText(hWnd, sb, sb.Capacity);
                    var title = sb.ToString();

                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        GetWindowRect(hWnd, out var rect);
                        windows.Add(new WindowInfo(
                            Title: title,
                            Handle: hWnd.ToString(),
                            X: rect.Left,
                            Y: rect.Top,
                            Width: rect.Right - rect.Left,
                            Height: rect.Bottom - rect.Top));
                    }
                }

                return true;
            }, IntPtr.Zero);

            return new WindowListResult(windows, windows.Count);
        });
    }

    [KernelFunction("manage_window")]
    [Description("Manages window operations including show, hide, minimize, maximize, and position changes.")]
    private async Task<string> ManageWindowAsync(
        [Description("Window title to find (exact match or partial match)")] string windowTitle,
        [Description("Action to perform: show, hide, minimize, maximize, restore, foreground, or move")]
        string action,
        [Description("X position for move action (optional)")] int? x = null,
        [Description("Y position for move action (optional)")] int? y = null,
        [Description("Width for move action (optional)")] int? width = null,
        [Description("Height for move action (optional)")] int? height = null)
    {
        _logger.LogDebug("Managing window: {Title}, Action: {Action}", windowTitle, action);

        return await Task.Run(() =>
        {
            IntPtr hWnd = IntPtr.Zero;

            // Find window by title
            EnumWindows((hwnd, lParam) =>
            {
                if (IsWindowVisible(hwnd))
                {
                    var sb = new StringBuilder(256);
                    GetWindowText(hwnd, sb, sb.Capacity);
                    var title = sb.ToString();

                    if (!string.IsNullOrWhiteSpace(title) && 
                        title.Contains(windowTitle, StringComparison.OrdinalIgnoreCase))
                    {
                        hWnd = hwnd;
                        return false; // Stop enumeration
                    }
                }

                return true;
            }, IntPtr.Zero);

            if (hWnd == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Window not found: {windowTitle}");
            }

            var actionLower = action.ToLowerInvariant();
            var success = actionLower switch
            {
                "show" => ShowWindow(hWnd, SW_SHOWNORMAL),
                "hide" => ShowWindow(hWnd, SW_HIDE),
                "minimize" => ShowWindow(hWnd, SW_SHOWMINIMIZED),
                "maximize" => ShowWindow(hWnd, SW_SHOWMAXIMIZED),
                "restore" => ShowWindow(hWnd, SW_RESTORE),
                "foreground" => SetForegroundWindow(hWnd),
                "move" => SetWindowPos(hWnd, IntPtr.Zero, x ?? 0, y ?? 0, width ?? 0, height ?? 0,
                    (width.HasValue && height.HasValue ? 0 : SWP_NOSIZE) |
                    (x.HasValue && y.HasValue ? 0 : SWP_NOMOVE) |
                    SWP_NOZORDER | SWP_SHOWWINDOW),
                _ => throw new ArgumentException($"Unknown action: {action}", nameof(action))
            };

            if (!success)
            {
                throw new InvalidOperationException($"Failed to perform action '{action}' on window '{windowTitle}'");
            }

            return $"Successfully performed '{action}' on window '{windowTitle}'";
        });
    }

    #endregion

    #region Clipboard Functions

    [KernelFunction("get_clipboard_text")]
    [Description("Gets the current text content from the system clipboard.")]
    private async Task<string> GetClipboardTextAsync()
    {
        _logger.LogDebug("Getting clipboard text");

        var text = await _clipboard.GetTextAsync();
        return text ?? string.Empty;
    }

    [KernelFunction("set_clipboard_text")]
    [Description("Sets text content to the system clipboard.")]
    private async Task<string> SetClipboardTextAsync(
        [Description("Text content to copy to clipboard")] string text)
    {
        _logger.LogDebug("Setting clipboard text");

        if (string.IsNullOrEmpty(text))
        {
            throw new ArgumentException("Text cannot be null or empty.", nameof(text));
        }

        await _clipboard.SetTextAsync(text);
        return $"Clipboard text set successfully ({text.Length} characters)";
    }

    #endregion

    #region System Settings Functions

    [KernelFunction("set_system_volume")]
    [Description("Sets the system master volume level (0-100).")]
    private async Task<string> SetSystemVolumeAsync(
        [Description("Volume level from 0 (mute) to 100 (maximum)")] int volumeLevel)
    {
        _logger.LogDebug("Setting system volume to: {Volume}", volumeLevel);

        if (volumeLevel < 0 || volumeLevel > 100)
        {
            throw new ArgumentException("Volume level must be between 0 and 100.", nameof(volumeLevel));
        }

        return await Task.Run(() =>
        {
            try
            {
                // Use Windows Core Audio API to set volume directly
                var script = $@"
                    Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;
[Guid(""5CDF2C82-841E-4546-9722-0CF74078229""), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioEndpointVolume {{
    int RegisterControlChangeNotify(IntPtr pNotify);
    int UnregisterControlChangeNotify(IntPtr pNotify);
    int GetChannelCount(ref uint pCount);
    int SetMasterVolumeLevel(float fLevelDB, IntPtr pguidEventContext);
    int SetMasterVolumeLevelScalar(float fLevel, IntPtr pguidEventContext);
    int GetMasterVolumeLevel(ref float pfLevelDB);
    int GetMasterVolumeLevelScalar(ref float pfLevel);
    int SetChannelVolumeLevel(uint nChannel, float fLevelDB, IntPtr pguidEventContext);
    int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, IntPtr pguidEventContext);
    int GetChannelVolumeLevel(uint nChannel, ref float pfLevelDB);
    int GetChannelVolumeLevelScalar(uint nChannel, ref float pfLevel);
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, IntPtr pguidEventContext);
    int GetMute(ref bool pbMute);
}}
[Guid(""F8679F50-850A-41CF-9C72-430F290290C8""), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumerator {{
    int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
    int GetDefaultAudioEndpoint(int dataFlow, int role, out IntPtr ppEndpoint);
    int GetDevice(string pwstrId, out IntPtr ppDevice);
    int RegisterEndpointNotificationCallback(IntPtr pNotify);
    int UnregisterEndpointNotificationCallback(IntPtr pNotify);
}}
[Guid(""A95664D2-9614-4F35-A746-DE8DB63617E6""), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice {{
    int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, out IntPtr ppInterface);
    int OpenPropertyStore(int stgmAccess, out IntPtr ppProperties);
    int GetId(out string ppstrId);
    int GetState(ref int pdwState);
}}
[ComImport, Guid(""BCDE0395-E52F-467C-8E3D-C4579291692E"")]
class MMDeviceEnumerator {{ }}
public class Audio {{
    public static void SetMasterVolume(float level) {{
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        enumerator.GetDefaultAudioEndpoint(0, 1, out IntPtr device);
        var dev = (IMMDevice)Marshal.GetObjectForIUnknown(device);
        dev.Activate(ref Guid.Parse(""5CDF2C82-841E-4546-9722-0CF74078229""), 23, IntPtr.Zero, out IntPtr endpointVolume);
        var vol = (IAudioEndpointVolume)Marshal.GetObjectForIUnknown(endpointVolume);
        vol.SetMasterVolumeLevelScalar(level / 100.0f, IntPtr.Zero);
        Marshal.ReleaseComObject(vol);
        Marshal.ReleaseComObject(dev);
        Marshal.ReleaseComObject(enumerator);
    }}
}}
'@
                    [Audio]::SetMasterVolume({volumeLevel})
                ";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -Command \"{script}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                process?.WaitForExit(5000);

                return $"System volume set to {volumeLevel}%";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set system volume");
                throw new InvalidOperationException($"Failed to set volume: {ex.Message}");
            }
        });
    }

    [KernelFunction("set_display_brightness")]
    [Description("Sets the display brightness level (0-100). Note: Only works on laptops and some monitors.")]
    private async Task<string> SetDisplayBrightnessAsync(
        [Description("Brightness level from 0 (darkest) to 100 (brightest)")] int brightnessLevel)
    {
        _logger.LogDebug("Setting display brightness to: {Brightness}", brightnessLevel);

        if (brightnessLevel < 0 || brightnessLevel > 100)
        {
            throw new ArgumentException("Brightness level must be between 0 and 100.", nameof(brightnessLevel));
        }

        return await Task.Run(() =>
        {
            try
            {
                var script = $@"
                    $brightness = {brightnessLevel}
                    (Get-WmiObject -Namespace root/WMI -Class WmiMonitorBrightnessMethods).WmiSetBrightness(1, $brightness)
                ";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -Command \"{script}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                process?.WaitForExit(5000);

                return $"Display brightness set to {brightnessLevel}%";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set display brightness");
                throw new InvalidOperationException($"Failed to set brightness. This feature may not be supported on your device: {ex.Message}");
            }
        });
    }

    [KernelFunction("set_desktop_wallpaper")]
    [Description("Sets the desktop wallpaper to the specified image file.")]
    private async Task<string> SetDesktopWallpaperAsync(
        [Description("Full path to the image file (supports jpg, png, bmp)")] string imagePath)
    {
        _logger.LogDebug("Setting desktop wallpaper to: {Path}", imagePath);

        return await Task.Run(() =>
        {
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Image file not found: {imagePath}");
            }

            // Verify it's an image file
            var ext = Path.GetExtension(imagePath).ToLowerInvariant();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".bmp")
            {
                throw new ArgumentException("Image file must be jpg, png, or bmp format.", nameof(imagePath));
            }

            int result = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, imagePath, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
            if (result == 0)
            {
                throw new InvalidOperationException("Failed to set desktop wallpaper.");
            }

            return $"Desktop wallpaper set successfully to: {Path.GetFileName(imagePath)}";
        });
    }

    #endregion

    #region Mouse and Keyboard Functions

    [KernelFunction("move_cursor")]
    [Description("Moves the mouse cursor to the specified screen coordinates.")]
    private async Task<string> MoveCursorAsync(
        [Description("X coordinate on screen")] int x,
        [Description("Y coordinate on screen")] int y)
    {
        _logger.LogDebug("Moving cursor to: ({X}, {Y})", x, y);

        return await Task.Run(() =>
        {
            if (!SetCursorPos(x, y))
            {
                throw new InvalidOperationException($"Failed to move cursor to ({x}, {y})");
            }

            return $"Cursor moved to ({x}, {y})";
        });
    }

    [KernelFunction("click_mouse")]
    [Description("Performs a mouse click at the current cursor position or at specified coordinates.")]
    private async Task<string> ClickMouseAsync(
        [Description("Button to click: left, right, or middle")] string button = "left",
        [Description("Optional X coordinate (moves cursor first if provided)")] int? x = null,
        [Description("Optional Y coordinate (moves cursor first if provided)")] int? y = null,
        [Description("Number of clicks (1 for single, 2 for double)")] int clickCount = 1)
    {
        _logger.LogDebug("Clicking mouse: {Button} button, {Count} times", button, clickCount);

        return await Task.Run(() =>
        {
            // Move cursor if coordinates provided
            if (x.HasValue && y.HasValue)
            {
                SetCursorPos(x.Value, y.Value);
                Thread.Sleep(50); // Small delay for cursor to settle
            }

            uint downFlag, upFlag;
            switch (button.ToLowerInvariant())
            {
                case "right":
                    downFlag = MOUSEEVENTF_RIGHTDOWN;
                    upFlag = MOUSEEVENTF_RIGHTUP;
                    break;
                case "middle":
                    downFlag = MOUSEEVENTF_MIDDLEDOWN;
                    upFlag = MOUSEEVENTF_MIDDLEUP;
                    break;
                default:
                    downFlag = MOUSEEVENTF_LEFTDOWN;
                    upFlag = MOUSEEVENTF_LEFTUP;
                    break;
            }

            for (int i = 0; i < clickCount; i++)
            {
                mouse_event(downFlag, 0, 0, 0, UIntPtr.Zero);
                Thread.Sleep(10);
                mouse_event(upFlag, 0, 0, 0, UIntPtr.Zero);
                if (i < clickCount - 1)
                {
                    Thread.Sleep(50);
                }
            }

            GetCursorPos(out var pos);
            return $"Clicked {button} button {clickCount} time(s) at ({pos.X}, {pos.Y})";
        });
    }

    [KernelFunction("send_keys")]
    [Description("Simulates keyboard input. Use special keys like {ENTER}, {TAB}, {ESC}, etc.")]
    private async Task<string> SendKeysAsync(
        [Description("Keys to send. Use {KEY} for special keys like {ENTER}, {TAB}, {CTRL}, {ALT}, {SHIFT}")] string keys)
    {
        _logger.LogDebug("Sending keys: {Keys}", keys);

        return await Task.Run(() =>
        {
            try
            {
                SendKeys.SendWait(keys);
                return $"Keys sent successfully: {keys}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send keys");
                throw new InvalidOperationException($"Failed to send keys: {ex.Message}");
            }
        });
    }

    #endregion

    #region Power Management Functions

    [KernelFunction("manage_power")]
    [Description("Manages system power state: sleep, hibernate, or shutdown.")]
    private async Task<string> ManagePowerAsync(
        [Description("Action: sleep, hibernate, shutdown, or restart")] string action)
    {
        _logger.LogDebug("Managing power: {Action}", action);

        return await Task.Run(() =>
        {
            var actionLower = action.ToLowerInvariant();

            try
            {
                switch (actionLower)
                {
                    case "sleep":
                        SetSuspendState(false, false, false);
                        return "System is going to sleep...";

                    case "hibernate":
                        SetSuspendState(true, false, false);
                        return "System is hibernating...";

                    case "shutdown":
                        Process.Start("shutdown", "/s /t 0");
                        return "System is shutting down...";

                    case "restart":
                        Process.Start("shutdown", "/r /t 0");
                        return "System is restarting...";

                    default:
                        throw new ArgumentException($"Unknown power action: {action}. Use: sleep, hibernate, shutdown, or restart.", nameof(action));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to manage power");
                throw new InvalidOperationException($"Failed to perform power action: {ex.Message}");
            }
        });
    }

    [KernelFunction("turn_off_monitor")]
    [Description("Turns off the monitor display (turns back on with mouse/keyboard input).")]
    private async Task<string> TurnOffMonitorAsync()
    {
        _logger.LogDebug("Turning off monitor");

        return await Task.Run(() =>
        {
            IntPtr hWnd = IntPtr.Zero; // HWND_BROADCAST
            SendMessage(hWnd, WM_SYSCOMMAND, (IntPtr)SC_MONITORPOWER, (IntPtr)MONITOR_OFF);
            return "Monitor turned off (move mouse or press key to turn back on)";
        });
    }

    #endregion

    #region Result Types

    [Serializable]
    private record WindowInfo(
        string Title,
        string Handle,
        int X,
        int Y,
        int Width,
        int Height);

    [Serializable]
    private record WindowListResult(IReadOnlyList<WindowInfo> Windows, int TotalCount);

    #endregion
}
