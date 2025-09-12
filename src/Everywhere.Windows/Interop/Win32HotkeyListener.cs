using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using Avalonia;
using Avalonia.Input;
using Everywhere.Configuration;
using Everywhere.Extensions;
using Everywhere.Interop;
using Everywhere.Utilities;
using Everywhere.Windows.Extensions;

namespace Everywhere.Windows.Interop;

public unsafe class Win32HotkeyListener : IHotkeyListener
{
    public event PointerHotkeyActivatedHandler PointerHotkeyActivated
    {
        add => _pointerHotkeyActivated += value;
        remove => _pointerHotkeyActivated -= value;
    }

    public event KeyboardHotkeyActivatedHandler KeyboardHotkeyActivated
    {
        add => _keyboardHotkeyActivated += value;
        remove => _keyboardHotkeyActivated -= value;
    }

    public KeyboardHotkey KeyboardHotkey
    {
        get => _keyboardHotkey;
        set => PInvoke.PostMessage(_hotkeyWindowHWnd, WM_REGISTER_HOTKEY, new WPARAM((nuint)value.Key), new LPARAM((nint)value.Modifiers));
    }

    private const nuint TimerId = 1;
    private const nuint InjectExtra = 0x0d000721;
    private const uint WM_REGISTER_HOTKEY = (uint)WINDOW_MESSAGE.WM_USER;

    private static PointerHotkeyActivatedHandler? _pointerHotkeyActivated;
    private static KeyboardHotkeyActivatedHandler? _keyboardHotkeyActivated;

    private static KeyboardHotkey _keyboardHotkey;
    private static LowLevelKeyboardHook? _keyboardHotkeyHook;
    private static KeyModifiers _pressedHookedModifiers;
    private static bool _isHotkeyTriggered;
    private static HWND _hotkeyWindowHWnd;
    private static bool _isHotkeyRegistered;

    private static uint _pressedXButton;
    private static bool _isXButtonEventTriggered;

    /// <summary>
    /// Indicates whether a keyboard hotkey scope is currently active.
    /// </summary>
    private static bool _isKeyboardHotkeyScoped;

    static Win32HotkeyListener()
    {
        new Thread(HookWindowMessageLoop).With(t =>
        {
            t.SetApartmentState(ApartmentState.STA);
            t.Name = "HotKeyThread";
        }).Start();
    }

    public IKeyboardHotkeyScope StartCaptureKeyboardHotkey() => new KeyboardHotkeyScopeImpl();

    private static void HookWindowMessageLoop()
    {
        using var hModule = PInvoke.GetModuleHandle(null);
        _hotkeyWindowHWnd = PInvoke.CreateWindowEx(
            WINDOW_EX_STYLE.WS_EX_NOACTIVATE,
            "STATIC",
            "Everywhere.HotKeyWindow",
            WINDOW_STYLE.WS_POPUP,
            0,
            0,
            0,
            0,
            HWND.Null,
            null,
            hModule,
            null);
        if (_hotkeyWindowHWnd.IsNull)
        {
            Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
        }

        MSG msg;
        while (PInvoke.GetMessage(&msg, HWND.Null, 0, 0) != 0)
        {
            switch (msg.message)
            {
                case WM_REGISTER_HOTKEY:
                {
                    var value = new KeyboardHotkey((Key)msg.wParam.Value, (KeyModifiers)msg.lParam.Value);
                    if (_keyboardHotkey == value) continue;

                    if (_isHotkeyRegistered) PInvoke.UnregisterHotKey(_hotkeyWindowHWnd, 0);
                    DisposeCollector.DisposeToDefault(ref _keyboardHotkeyHook);

                    if (value.Modifiers == KeyModifiers.None || value.Key == Key.None)
                    {
                        // invalid hotkey, ignore
                        _keyboardHotkey = default;
                        continue;
                    }

                    _keyboardHotkey = value;

                    if (value.Modifiers.HasFlag(KeyModifiers.Meta))
                    {
                        // for those keys with Meta modifier, we need a low level keyboard hook to block the Windows key down event
                        _keyboardHotkeyHook = new LowLevelKeyboardHook(KeyboardHotkeyHookProc);
                    }
                    else
                    {
                        var hotKeyModifiers = HOT_KEY_MODIFIERS.MOD_NOREPEAT;
                        if (value.Modifiers.HasFlag(KeyModifiers.Control))
                            hotKeyModifiers |= HOT_KEY_MODIFIERS.MOD_CONTROL;
                        if (value.Modifiers.HasFlag(KeyModifiers.Shift))
                            hotKeyModifiers |= HOT_KEY_MODIFIERS.MOD_SHIFT;
                        if (value.Modifiers.HasFlag(KeyModifiers.Alt))
                            hotKeyModifiers |= HOT_KEY_MODIFIERS.MOD_ALT;
                        _isHotkeyRegistered = PInvoke.RegisterHotKey(
                            _hotkeyWindowHWnd,
                            0,
                            hotKeyModifiers,
                            (uint)value.Key.ToVirtualKey()
                        );
                    }

                    break;
                }
                case (uint)WINDOW_MESSAGE.WM_HOTKEY when !_isKeyboardHotkeyScoped:
                {
                    _keyboardHotkeyActivated?.Invoke();
                    break;
                }
                case (uint)WINDOW_MESSAGE.WM_TIMER when msg.wParam == TimerId:
                {
                    _isXButtonEventTriggered = true;
                    PInvoke.KillTimer(_hotkeyWindowHWnd, TimerId);

                    if (_pointerHotkeyActivated is not { } handler) continue;
                    PInvoke.GetCursorPos(out var point);
                    handler.Invoke(new PixelPoint(point.X, point.Y));
                    break;
                }
            }

            PInvoke.DispatchMessage(&msg);
        }

        // Cleanup
        if (_isHotkeyRegistered) PInvoke.UnregisterHotKey(_hotkeyWindowHWnd, 0);
        DisposeCollector.DisposeToDefault(ref _keyboardHotkeyHook);

        PInvoke.DestroyWindow(_hotkeyWindowHWnd);
    }

    private static void KeyboardHotkeyHookProc(UIntPtr wParam, ref KBDLLHOOKSTRUCT lParam, ref bool blockNext)
    {
        if (_isKeyboardHotkeyScoped) return;

        var virtualKey = (VIRTUAL_KEY)lParam.vkCode;
        var key = virtualKey.ToAvaloniaKey();
        var keyModifiers = virtualKey.ToKeyModifiers();

        switch ((WINDOW_MESSAGE)wParam)
        {
            case WINDOW_MESSAGE.WM_KEYDOWN or WINDOW_MESSAGE.WM_SYSKEYDOWN:
            {
                // If a modifier key is pressed, add it to our state.
                if (keyModifiers != KeyModifiers.None)
                {
                    _pressedHookedModifiers |= keyModifiers;
                }

                // Check if the pressed key is the main key of the hotkey,
                // all required modifiers are pressed, and the hotkey hasn't been triggered yet.
                var isMainKeyPressed = key == _keyboardHotkey.Key && key != Key.None;
                var areModifiersPressed = _pressedHookedModifiers == _keyboardHotkey.Modifiers;

                if (!_isHotkeyTriggered && isMainKeyPressed && areModifiersPressed)
                {
                    // Mark as triggered to prevent re-firing.
                    _isHotkeyTriggered = true;
                    // Block this key event.
                    blockNext = true;
                    // Invoke the hotkey activated event.
                    _keyboardHotkeyActivated?.Invoke();
                    return;
                }

                // If any part of the hotkey combination is pressed, block the event.
                // This is to prevent side effects, e.g., opening the Start Menu with the Win key.
                if ((_pressedHookedModifiers & _keyboardHotkey.Modifiers) != 0 || isMainKeyPressed)
                {
                    blockNext = true;
                }

                break;
            }
            case WINDOW_MESSAGE.WM_KEYUP or WINDOW_MESSAGE.WM_SYSKEYUP:
            {
                // If a modifier key is released, remove it from our state.
                if (keyModifiers != KeyModifiers.None)
                {
                    _pressedHookedModifiers &= ~keyModifiers;
                }

                // If all modifier keys are released, reset the triggered flag.
                // This allows the hotkey to be triggered again.
                if (_pressedHookedModifiers == KeyModifiers.None)
                {
                    _isHotkeyTriggered = false;
                }

                // Block the key up event if it was part of the hotkey combination
                // to ensure consistent behavior.
                if ((_keyboardHotkey.Modifiers.HasFlag(keyModifiers) && keyModifiers != KeyModifiers.None) || key == _keyboardHotkey.Key)
                {
                    blockNext = true;
                }
                break;
            }
        }
    }

    private static LRESULT MouseHookProc(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code < 0) return PInvoke.CallNextHookEx(null, code, wParam, lParam);

        ref var hookStruct = ref Unsafe.AsRef<MSLLHOOKSTRUCT>(lParam.Value.ToPointer());
        if (hookStruct.dwExtraInfo == InjectExtra)
            return PInvoke.CallNextHookEx(null, code, wParam, lParam);

        var button = hookStruct.mouseData >> 16 & 0xFFFF;
        switch (wParam.Value)
        {
            case (uint)WINDOW_MESSAGE.WM_XBUTTONDOWN when button is PInvoke.XBUTTON1:
            {
                _pressedXButton = button;
                PInvoke.SetTimer(_hotkeyWindowHWnd, TimerId, 500, null);
                return new LRESULT(1); // block XButton1 down event
            }
            case (uint)WINDOW_MESSAGE.WM_XBUTTONUP when button == _pressedXButton:
            {
                _pressedXButton = 0;

                if (_isXButtonEventTriggered)
                {
                    _isXButtonEventTriggered = false;
                    return new LRESULT(1); // block XButton1 up event
                }

                PInvoke.KillTimer(_hotkeyWindowHWnd, TimerId);

                // send XButton1 down and up event to the system in new thread
                // otherwise it will cause a deadlock
                Task.Run(() =>
                {
                    PInvoke.SendInput(
                        [
                            new INPUT
                            {
                                type = INPUT_TYPE.INPUT_MOUSE,
                                Anonymous = new INPUT._Anonymous_e__Union
                                {
                                    mi = new MOUSEINPUT
                                    {
                                        dwFlags = MOUSE_EVENT_FLAGS.MOUSEEVENTF_XDOWN,
                                        mouseData = button,
                                        dwExtraInfo = InjectExtra
                                    }
                                }
                            },
                            new INPUT
                            {
                                type = INPUT_TYPE.INPUT_MOUSE,
                                Anonymous = new INPUT._Anonymous_e__Union
                                {
                                    mi = new MOUSEINPUT
                                    {
                                        dwFlags = MOUSE_EVENT_FLAGS.MOUSEEVENTF_XUP,
                                        mouseData = button,
                                        dwExtraInfo = InjectExtra
                                    }
                                }
                            },
                        ],
                        sizeof(INPUT));
                });

                break;
            }
        }

        return PInvoke.CallNextHookEx(null, code, wParam, lParam);
    }

    private class KeyboardHotkeyScopeImpl : IKeyboardHotkeyScope
    {
        public KeyboardHotkey PressingHotkey { get; private set; }

        private readonly PropertyChangingEventArgs _changingEventArgsCache = new(nameof(PressingHotkey));
        private readonly PropertyChangedEventArgs _changedEventArgsCache = new(nameof(PressingHotkey));
        private readonly LowLevelKeyboardHook _keyboardHook;

        private KeyModifiers _pressedKeyModifiers = KeyModifiers.None;

        public KeyboardHotkeyScopeImpl()
        {
            if (_isKeyboardHotkeyScoped)
            {
                throw new InvalidOperationException("Only one keyboard hotkey scope can be active at a time.");
            }

            _keyboardHook = new LowLevelKeyboardHook(KeyboardHookCallback);
            _isKeyboardHotkeyScoped = true;
        }

        private void KeyboardHookCallback(UIntPtr wParam, ref KBDLLHOOKSTRUCT lParam, ref bool blockNext)
        {
            var virtualKey = (VIRTUAL_KEY)lParam.vkCode;
            var keyModifiers = virtualKey.ToKeyModifiers();

            switch ((WINDOW_MESSAGE)wParam)
            {
                case WINDOW_MESSAGE.WM_KEYDOWN when keyModifiers == KeyModifiers.None:
                {
                    PressingHotkey = PressingHotkey with { Key = virtualKey.ToAvaloniaKey() };
                    PropertyChanging?.Invoke(this, _changingEventArgsCache);
                    break;
                }
                case WINDOW_MESSAGE.WM_KEYDOWN:
                {
                    _pressedKeyModifiers |= keyModifiers;
                    PressingHotkey = PressingHotkey with { Modifiers = _pressedKeyModifiers };
                    PropertyChanging?.Invoke(this, _changingEventArgsCache);
                    break;
                }
                case WINDOW_MESSAGE.WM_KEYUP:
                {
                    _pressedKeyModifiers &= ~keyModifiers;
                    if (_pressedKeyModifiers == KeyModifiers.None)
                    {
                        if (PressingHotkey.Modifiers != KeyModifiers.None && PressingHotkey.Key == Key.None)
                        {
                            PressingHotkey = default; // modifiers only hotkey, reset it
                        }

                        // system key is all released, capture is done
                        PropertyChanging?.Invoke(this, _changingEventArgsCache);
                        PropertyChanged?.Invoke(this, _changedEventArgsCache);
                    }
                    break;
                }
            }

            blockNext = true;
        }

        public void Dispose()
        {
            _keyboardHook.Dispose();
            _isKeyboardHotkeyScoped = false;
        }

        public event PropertyChangingEventHandler? PropertyChanging;
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}