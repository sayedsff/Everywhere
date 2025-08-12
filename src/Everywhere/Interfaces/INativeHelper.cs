using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace Everywhere.Interfaces;

public interface INativeHelper
{
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

    void RestartAsAdministrator();

    /// <summary>
    /// Make the window cannot be focused by the system.
    /// </summary>
    /// <param name="window"></param>
    void SetWindowNoFocus(Window window);

    void SetWindowAutoHide(Window window);

    void SetWindowHitTestInvisible(Window window);

    void SetWindowCornerRadius(Window window, CornerRadius cornerRadius);

    void HideWindowWithoutAnimation(Window window);

    /// <summary>
    /// Get the bitmap from the clipboard. This method is asynchronous and may return null if the clipboard does not contain a bitmap.
    /// </summary>
    /// <returns></returns>
    Task<WriteableBitmap?> GetClipboardBitmapAsync();

    void ShowDesktopNotification(string message, string? title = null);
}