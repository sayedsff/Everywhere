using Avalonia.Controls;

namespace Everywhere.Interop;

/// <summary>
/// Provides helper methods for interacting with application windows.
/// </summary>
public interface IWindowHelper
{
    /// <summary>
    /// Set whether the window is focusable or not.
    /// </summary>
    /// <param name="window"></param>
    /// <param name="focusable"></param>
    void SetFocusable(Window window, bool focusable);

    /// <summary>
    /// Set whether the window is hit-test visible (interactive) or not.
    /// </summary>
    /// <param name="window"></param>
    /// <param name="visible"></param>
    void SetHitTestVisible(Window window, bool visible);

    /// <summary>
    /// Get whether the window is effectively visible (taking into account cloaking and other factors).
    /// </summary>
    /// <param name="window"></param>
    /// <returns></returns>
    bool GetEffectiveVisible(Window window);

    /// <summary>
    /// Set whether the window is cloaked (invisible and non-interactive, without any animation).
    /// </summary>
    /// <param name="window"></param>
    /// <param name="cloaked"></param>
    void SetCloaked(Window window, bool cloaked);

    /// <summary>
    /// Get whether any dialog is opened on the given window. (e.g. MessageBox, OpenFileDialog, etc.)
    /// </summary>
    /// <param name="window"></param>
    /// <returns></returns>
    bool AnyModelDialogOpened(Window window);
}