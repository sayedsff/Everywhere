using Everywhere.Configuration;

namespace Everywhere.Interop;

public delegate void PointerHotkeyActivatedHandler(PixelPoint point);
public delegate void KeyboardHotkeyActivatedHandler();

public interface IHotkeyListener
{
    event PointerHotkeyActivatedHandler PointerHotkeyActivated;

    event KeyboardHotkeyActivatedHandler KeyboardHotkeyActivated;

    /// <summary>
    /// When set, the listener will capture the keyboard hotkey and raise the <see cref="KeyboardHotkeyActivated"/> event
    /// </summary>
    KeyboardHotkey KeyboardHotkey { get; set; }

    /// <summary>
    /// Starts capturing the keyboard hotkey
    /// </summary>
    /// <returns></returns>
    IKeyboardHotkeyScope StartCaptureKeyboardHotkey();
}

/// <summary>
/// Represents a scope for capturing keyboard hotkeys.
/// </summary>
public interface IKeyboardHotkeyScope : IDisposable
{
    /// <summary>
    /// Raised when the hotkey is changed during capturing.
    /// e.g., when the user is pressing ctrl+alt+K, this event will be raised when ctrl is pressed, then alt is pressed, then K is pressed.
    /// </summary>
    delegate void PressingHotkeyChangedHandler(IKeyboardHotkeyScope scope, KeyboardHotkey currentHotkey);

    delegate void HotkeyFinishedHandler(IKeyboardHotkeyScope scope, KeyboardHotkey finalHotkey);

    KeyboardHotkey PressingHotkey { get; }

    bool IsDisposed { get; }

    event PressingHotkeyChangedHandler PressingHotkeyChanged;

    event HotkeyFinishedHandler HotkeyFinished;
}