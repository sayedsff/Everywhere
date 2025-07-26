using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace Everywhere.Interfaces;

public interface INativeHelper
{
    /// <summary>
    /// Make the window cannot be focused by the system.
    /// </summary>
    /// <param name="window"></param>
    void SetWindowNoFocus(Window window);

    void SetWindowAutoHide(Window window);

    void SetWindowHitTestInvisible(Window window);

    void SetWindowCornerRadius(Window window, CornerRadius cornerRadius);

    void HideWindowWithoutAnimation(Window window);

    WriteableBitmap? GetClipboardBitmap();
}