using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using Avalonia;
using Avalonia.Threading;
using Everywhere.Extensions;
using Everywhere.Interfaces;
using Everywhere.Windows.Interop;

namespace Everywhere.Windows.Services;

public unsafe class Win32UserInputTrigger : IUserInputTrigger
{
    public event IUserInputTrigger.KeyboardHotkeyActivatedHandler KeyboardHotkeyActivated
    {
        add => keyboardHotkeyActivated += value;
        remove => keyboardHotkeyActivated -= value;
    }

    private static IUserInputTrigger.KeyboardHotkeyActivatedHandler? keyboardHotkeyActivated;

    public event IUserInputTrigger.PointerHotkeyActivatedHandler PointerHotkeyActivated
    {
        add => pointerHotkeyActivated += value;
        remove => pointerHotkeyActivated -= value;
    }

    private static IUserInputTrigger.PointerHotkeyActivatedHandler? pointerHotkeyActivated;

    private static HWND hotkeyWindowHWnd;
    private static uint pressedXButton;
    private static bool isXButtonEventTriggered;

    private const nuint TimerId = 1;
    private const nuint InjectExtra = 0x0d000721;

    static Win32UserInputTrigger()
    {
        new Thread(HookThreadWork).With(
            t =>
            {
                t.SetApartmentState(ApartmentState.STA);
                t.Name = "HotKeyThread";
            }).Start();
    }

    private static void HookThreadWork()
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

        // Set up the hotkey
        PInvoke.RegisterHotKey(
            hotkeyWindowHWnd,
            0,
            HOT_KEY_MODIFIERS.MOD_CONTROL,
            (uint)VIRTUAL_KEY.VK_TAB
        );



        MSG msg;
        while (PInvoke.GetMessage(&msg, HWND.Null, 0, 0) != 0)
        {
            switch (msg.message)
            {
                case (uint)WINDOW_MESSAGE.WM_HOTKEY:
                {
                    Dispatcher.UIThread.Post(() => keyboardHotkeyActivated?.Invoke());
                    break;
                }
                case (uint)WINDOW_MESSAGE.WM_TIMER when msg.wParam == TimerId:
                {
                    isXButtonEventTriggered = true;
                    PInvoke.KillTimer(hotkeyWindowHWnd, TimerId);

                    Dispatcher.UIThread.Post(
                        () =>
                        {
                            if (pointerHotkeyActivated is not { } handler) return;
                            PInvoke.GetCursorPos(out var point);
                            handler.Invoke(new PixelPoint(point.X, point.Y));
                        });
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
                Task.Run(
                    () =>
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
}