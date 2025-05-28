using System.ComponentModel;
using Everywhere.Models;

namespace Everywhere.Interfaces;

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

public interface IKeyboardHotkeyScope : INotifyPropertyChanging, INotifyPropertyChanged, IDisposable
{
    KeyboardHotkey PressingHotkey { get; }
}