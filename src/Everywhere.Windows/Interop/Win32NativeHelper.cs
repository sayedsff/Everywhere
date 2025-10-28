using System.Diagnostics;
using System.Security.Principal;
using Windows.Data.Xml.Dom;
using Windows.UI.Composition;
using Windows.UI.Notifications;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Everywhere.Common;
using Everywhere.Extensions;
using Everywhere.Interop;
using Microsoft.Win32;
using Vector = Avalonia.Vector;
using Visual = Windows.UI.Composition.Visual;

namespace Everywhere.Windows.Interop;

public class Win32NativeHelper : INativeHelper
{
    private const string AppName = nameof(Everywhere);
    private const string RegistryInstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{D66EA41B-8DEB-4E5A-9D32-AB4F8305F664}}_is1";
    private const string RegistryRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private static string ProcessPathWithArgument => $"\"{Environment.ProcessPath}\" --autorun";

    public bool IsInstalled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryInstallKey);
            return key?.GetValue("InstallLocation")?.ToString() is not null;
        }
    }

    public bool IsAdministrator
    {
        get
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public bool IsUserStartupEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey);
                return key?.GetValue(AppName) != null;
            }
            catch
            {
                // If the registry key cannot be accessed, assume it is not enabled.
                return false;
            }
        }
        set
        {
            if (value)
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, true);
                key?.SetValue(AppName, ProcessPathWithArgument);
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, true);
                key?.DeleteValue(AppName, false);
            }
        }
    }

    public bool IsAdministratorStartupEnabled
    {
        get
        {
            try
            {
                return TaskSchedulerHelper.IsTaskScheduled(AppName);
            }
            catch
            {
                return false;
            }
        }
        set
        {
            if (!IsAdministrator) throw new UnauthorizedAccessException("The current user is not an administrator.");

            if (value)
            {
                TaskSchedulerHelper.CreateScheduledTask(AppName, ProcessPathWithArgument);
            }
            else
            {
                TaskSchedulerHelper.DeleteScheduledTask(AppName);
            }
        }
    }

    public void RestartAsAdministrator()
    {
        if (IsAdministrator)
        {
            throw new InvalidOperationException("The application is already running as an administrator.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath.NotNull(),
            Arguments = "--ui",
            UseShellExecute = true,
            Verb = "runas" // This will prompt for elevation
        };

        try
        {
            Entrance.ReleaseMutex();
            Process.Start(startInfo);
            Environment.Exit(0); // Exit the current process
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to restart as administrator.", ex);
        }
    }

    public unsafe void HideWindowWithoutAnimation(Window window)
    {
        BOOL disableTransitions = true;
        var hWnd = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
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

    private readonly Lock _clipboardLock = new();

    public unsafe Task<WriteableBitmap?> GetClipboardBitmapAsync() => Task.Run(() =>
    {
        using var _ = _clipboardLock.EnterScope();

        if (!PInvoke.OpenClipboard(HWND.Null)) return null;

        var hDc = PInvoke.GetDC(HWND.Null);
        try
        {
            using var hBitmap = PInvoke.GetClipboardData_SafeHandle(2); // CF_BITMAP
            if (hBitmap.IsInvalid) return null;

            var bmp = new BITMAP();
            PInvoke.GetObject(hBitmap, sizeof(BITMAP), &bmp);
            var width = bmp.bmWidth;
            var height = bmp.bmHeight;
            if (width <= 0 || height <= 0) return null;

            var bitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Unpremul);
            using var buffer = bitmap.Lock();

            var bmi = new BITMAPINFO();
            bmi.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
            bmi.bmiHeader.biWidth = width;
            bmi.bmiHeader.biHeight = -height; // 负值表示自顶向下
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = (int)BI_COMPRESSION.BI_RGB;

            PInvoke.GetDIBits(
                hDc,
                hBitmap,
                0U,
                (uint)height,
                buffer.Address.ToPointer(),
                &bmi,
                DIB_USAGE.DIB_RGB_COLORS
            );

            return bitmap;
        }
        finally
        {
            if (hDc != HDC.Null) PInvoke.ReleaseDC(HWND.Null, hDc);
            PInvoke.CloseClipboard();
        }
    });

    public void ShowDesktopNotification(string message, string? title)
    {
        var registryKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\AppUserModelId");
        const string ModelId = "{D66EA41B-8DEB-4E5A-9D32-AB4F8305F664}/Everywhere";
        var tempFilePath = Path.Combine(Path.GetTempPath(), "D66EA41B-8DEB-4E5A-9D32-AB4F8305F664-Everywhere.ico");

        using (var subKey = registryKey.CreateSubKey(ModelId))
        {
            subKey.SetValue("DisplayName", "Everywhere");

            var iconResource = AssetLoader.Open(new Uri("avares://Everywhere/Assets/Everywhere.ico"));
            using (var fs = File.Create(tempFilePath))
            {
                iconResource.CopyTo(fs);
            }

            subKey.SetValue("IconUri", tempFilePath);
        }

        var xml =
            $"""
             <toast launch='conversationId=9813'>
                 <visual>
                     <binding template='ToastGeneric'>
                         {(string.IsNullOrEmpty(title) ? "" : $"<text>{title}</text>")}
                         <text>{message}</text>
                     </binding>
                 </visual>
             </toast>
             """;
        var xmlDocument = new XmlDocument();
        xmlDocument.LoadXml(xml);

        var toast = new ToastNotification(xmlDocument);
        ToastNotificationManager.CreateToastNotifier(ModelId).Show(toast);

        toast.Dismissed += delegate
        {
            try
            {
                registryKey.DeleteSubKey(ModelId);
                registryKey.Dispose();
                File.Delete(tempFilePath);
            }
            catch
            {
                // ignore
            }
        };
    }

    public void OpenFileLocation(string fullPath)
    {
        if (fullPath.IsNullOrWhiteSpace()) return;
        var args = $"/e,/select,\"{fullPath}\"";
        Process.Start(new ProcessStartInfo("explorer.exe", args) { UseShellExecute = true });
    }
}