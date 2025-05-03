using Avalonia.Controls;

namespace Everywhere.Interfaces;

public interface IPlatformHelper
{
    /// <summary>
    /// Make the window cannot be focused by the system.
    /// </summary>
    /// <param name="window"></param>
    void SetWindowNoFocus(Window window);

    void SetWindowAutoHide(Window window);
}