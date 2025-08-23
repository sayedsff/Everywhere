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
using Everywhere.Windows.Extensions;

namespace Everywhere.Windows.Interop;

public unsafe class Win32HotkeyListener : IHotkeyListener
{
    public event PointerHotkeyActivatedHandler PointerHotkeyActivated
    {
        add => pointerHotkeyActivated += value;
        remove => pointerHotkeyActivated -= value;
    }

    public event KeyboardHotkeyActivatedHandler KeyboardHotkeyActivated
    {
        add => keyboardHotkeyActivated += value;
        remove => keyboardHotkeyActivated -= value;
    }

    public KeyboardHotkey KeyboardHotkey
    {
        get => keyboardHotkey;
        set => PInvoke.PostMessage(hotkeyWindowHWnd, WM_REGISTER_HOTKEY, new WPARAM((nuint)value.Key), new LPARAM((nint)value.Modifiers));
    }

    private const nuint TimerId = 1;
    private const nuint InjectExtra = 0x0d000721;
    private const uint WM_REGISTER_HOTKEY = (uint)WINDOW_MESSAGE.WM_USER;

    private static PointerHotkeyActivatedHandler? pointerHotkeyActivated;
    private static KeyboardHotkeyActivatedHandler? keyboardHotkeyActivated;
    private static KeyboardHotkey keyboardHotkey;

    private static HWND hotkeyWindowHWnd;
    private static uint pressedXButton;
    private static bool isXButtonEventTriggered;

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
        hotkeyWindowHWnd = PInvoke.CreateWindowEx(
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
        if (hotkeyWindowHWnd.IsNull)
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
                    if (keyboardHotkey == value) continue;

                    if (!keyboardHotkey.IsEmpty)
                    {
                        PInvoke.UnregisterHotKey(hotkeyWindowHWnd, 0);
                    }

                    if (value.Modifiers == KeyModifiers.None || value.Key == Key.None)
                    {
                        // invalid hotkey, ignore
                        keyboardHotkey = default;
                        continue;
                    }

                    keyboardHotkey = value;

                    var hotKeyModifiers = HOT_KEY_MODIFIERS.MOD_NOREPEAT;
                    if (value.Modifiers.HasFlag(KeyModifiers.Control))
                        hotKeyModifiers |= HOT_KEY_MODIFIERS.MOD_CONTROL;
                    if (value.Modifiers.HasFlag(KeyModifiers.Shift))
                        hotKeyModifiers |= HOT_KEY_MODIFIERS.MOD_SHIFT;
                    if (value.Modifiers.HasFlag(KeyModifiers.Alt))
                        hotKeyModifiers |= HOT_KEY_MODIFIERS.MOD_ALT;
                    if (value.Modifiers.HasFlag(KeyModifiers.Meta))
                        hotKeyModifiers |= HOT_KEY_MODIFIERS.MOD_WIN;
                    PInvoke.RegisterHotKey(
                        hotkeyWindowHWnd,
                        0,
                        hotKeyModifiers,
                        (uint)value.Key.ToVirtualKey()
                    );
                    break;
                }
                case (uint)WINDOW_MESSAGE.WM_HOTKEY:
                {
                    keyboardHotkeyActivated?.Invoke();
                    break;
                }
                case (uint)WINDOW_MESSAGE.WM_TIMER when msg.wParam == TimerId:
                {
                    isXButtonEventTriggered = true;
                    PInvoke.KillTimer(hotkeyWindowHWnd, TimerId);

                    if (pointerHotkeyActivated is not { } handler) continue;
                    PInvoke.GetCursorPos(out var point);
                    handler.Invoke(new PixelPoint(point.X, point.Y));
                    break;
                }
            }

            PInvoke.DispatchMessage(&msg);
        }

        PInvoke.DestroyWindow(hotkeyWindowHWnd);
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
                pressedXButton = button;
                PInvoke.SetTimer(hotkeyWindowHWnd, TimerId, 500, null);
                return new LRESULT(1); // block XButton1 down event
            }
            case (uint)WINDOW_MESSAGE.WM_XBUTTONUP when button == pressedXButton:
            {
                pressedXButton = 0;

                if (isXButtonEventTriggered)
                {
                    isXButtonEventTriggered = false;
                    return new LRESULT(1); // block XButton1 up event
                }

                PInvoke.KillTimer(hotkeyWindowHWnd, TimerId);

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

        private readonly PropertyChangingEventArgs changingEventArgsCache = new(nameof(PressingHotkey));
        private readonly PropertyChangedEventArgs changedEventArgsCache = new(nameof(PressingHotkey));
        private readonly LowLevelKeyboardHook keyboardHook;

        private KeyModifiers pressedKeyModifiers = KeyModifiers.None;

        public KeyboardHotkeyScopeImpl()
        {
            keyboardHook = new LowLevelKeyboardHook(KeyboardHookCallback);
        }

        private void KeyboardHookCallback(UIntPtr wParam, ref KBDLLHOOKSTRUCT lParam, ref bool blockNext)
        {
            var virtualKey = (VIRTUAL_KEY)lParam.vkCode;
            var systemKey = virtualKey switch
            {
                VIRTUAL_KEY.VK_CONTROL or VIRTUAL_KEY.VK_LCONTROL or VIRTUAL_KEY.VK_RCONTROL => KeyModifiers.Control,
                VIRTUAL_KEY.VK_SHIFT or VIRTUAL_KEY.VK_LSHIFT or VIRTUAL_KEY.VK_RSHIFT => KeyModifiers.Shift,
                VIRTUAL_KEY.VK_MENU or VIRTUAL_KEY.VK_LMENU or VIRTUAL_KEY.VK_RMENU => KeyModifiers.Alt,
                VIRTUAL_KEY.VK_LWIN or VIRTUAL_KEY.VK_RWIN => KeyModifiers.Meta,
                _ => KeyModifiers.None
            };

            switch ((WINDOW_MESSAGE)wParam)
            {
                case WINDOW_MESSAGE.WM_KEYDOWN when systemKey == KeyModifiers.None:
                {
                    PressingHotkey = PressingHotkey with { Key = virtualKey.ToAvaloniaKey() };
                    PropertyChanging?.Invoke(this, changingEventArgsCache);
                    break;
                }
                case WINDOW_MESSAGE.WM_KEYDOWN:
                {
                    pressedKeyModifiers |= systemKey;
                    PressingHotkey = PressingHotkey with { Modifiers = pressedKeyModifiers };
                    PropertyChanging?.Invoke(this, changingEventArgsCache);
                    break;
                }
                case WINDOW_MESSAGE.WM_KEYUP:
                {
                    pressedKeyModifiers &= ~systemKey;
                    if (pressedKeyModifiers == KeyModifiers.None)
                    {
                        if (PressingHotkey.Modifiers != KeyModifiers.None && PressingHotkey.Key == Key.None)
                        {
                            PressingHotkey = default; // modifiers only hotkey, reset it
                        }

                        // system key is all released, capture is done
                        PropertyChanging?.Invoke(this, changingEventArgsCache);
                        PropertyChanged?.Invoke(this, changedEventArgsCache);
                    }
                    break;
                }
            }

            blockNext = true;
        }

        public void Dispose()
        {
            keyboardHook.Dispose();
        }

        public event PropertyChangingEventHandler? PropertyChanging;
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}