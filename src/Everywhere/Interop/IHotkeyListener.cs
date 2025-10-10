namespace Everywhere.Interop;

public interface IHotkeyListener
{
    // Register a keyboard hotkey. Multiple handlers for the same hotkey are supported.
    // Returns an IDisposable that unregisters this handler only.
    IDisposable Register(KeyboardHotkey hotkey, Action handler);

    // Register a mouse hotkey. Multiple handlers for the same MouseKey (with different delays) are supported.
    // Returns an IDisposable that unregisters this handler only.
    IDisposable Register(MouseHotkey hotkey, Action handler);

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