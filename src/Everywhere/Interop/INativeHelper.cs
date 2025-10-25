using Avalonia.Media.Imaging;

namespace Everywhere.Interop;

public interface INativeHelper
{
    /// <summary>
    /// Check if the application is installed in the system.
    /// </summary>
    bool IsInstalled { get; }

    /// <summary>
    /// Check if the current user is an administrator (aka UAC on Windows).
    /// </summary>
    bool IsAdministrator { get; }

    /// <summary>
    /// Check if the application is set to start with the system as User.
    /// </summary>
    bool IsUserStartupEnabled { get; set; }

    /// <summary>
    /// Check if the application is set to start with the system as Administrator (aka UAC on Windows).
    /// This can only be set if the current user is an administrator.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Thrown if the current user is not an administrator.</exception>
    bool IsAdministratorStartupEnabled { get; set; }

    /// <summary>
    /// Restart the application as administrator (aka UAC on Windows).
    /// </summary>
    void RestartAsAdministrator();

    /// <summary>
    /// Get the bitmap from the clipboard. This method is asynchronous and may return null if the clipboard does not contain a bitmap.
    /// </summary>
    /// <returns></returns>
    Task<WriteableBitmap?> GetClipboardBitmapAsync();

    /// <summary>
    /// Show a desktop notification with the given message and optional title.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="title"></param>
    void ShowDesktopNotification(string message, string? title = null);

    /// <summary>
    /// Open the file location in the system file explorer and select the file.
    /// </summary>
    /// <param name="fullPath"></param>
    void OpenFileLocation(string fullPath);
}