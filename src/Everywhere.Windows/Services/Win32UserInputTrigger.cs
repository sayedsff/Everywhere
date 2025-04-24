using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using Everywhere.Interfaces;

namespace Everywhere.Windows.Services;

public unsafe class Win32UserInputTrigger : IUserInputTrigger
{
    public event Action? ActionPanelRequested;

    #region Hotkey

    // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
    // If this field is local, the delegate will be garbage collected, then the app will crash
    private readonly WNDPROC lpHotKeyWndProc;
    private readonly HWND hHotkeyWindow;

    #endregion

    #region MouseHook

    private readonly UnhookWindowsHookExSafeHandle mouseHookHandle;
    private readonly Timer mouseHookTimer;

    private bool isProcessingMouseHook;
    private uint pressedXButton;
    private bool isXButtonEventTriggered;

    #endregion

    public Win32UserInputTrigger()
    {
        using var hModule = PInvoke.GetModuleHandle(null);

        // Set up the hotkey
        fixed (char* lpClassName = "Everywhere.HotKeyWindowClass")
        fixed (char* lpWindowName = "Everywhere.HotKeyWindow")
        {
            lpHotKeyWndProc = HotKeyWindowProc;
            var result = PInvoke.RegisterClassEx(
                new WNDCLASSEXW
                {
                    cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                    lpfnWndProc = lpHotKeyWndProc,
                    hInstance = (HINSTANCE)hModule.DangerousGetHandle(),
                    lpszClassName = lpClassName
                });
            if (result == 0)
            {
                Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
            }

            hHotkeyWindow = PInvoke.CreateWindowEx(
                WINDOW_EX_STYLE.WS_EX_NOACTIVATE,
                lpClassName,
                lpWindowName,
                WINDOW_STYLE.WS_POPUP,
                0,
                0,
                0,
                0,
                HWND.Null,
                HMENU.Null,
                (HINSTANCE)hModule.DangerousGetHandle(),
                null);
            if (hHotkeyWindow.IsNull)
            {
                Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
            }
        }

        PInvoke.RegisterHotKey(
            hHotkeyWindow,
            0,
            HOT_KEY_MODIFIERS.MOD_CONTROL,
            (uint)VIRTUAL_KEY.VK_TAB
        );

        // Set up the low-level mouse hook
        mouseHookHandle = new UnhookWindowsHookExSafeHandle();
        // mouseHookHandle = PInvoke.SetWindowsHookEx(
        //     WINDOWS_HOOK_ID.WH_MOUSE_LL,
        //     MouseHookProc,
        //     PInvoke.GetModuleHandle(hModule.ModuleName),
        //     0);
        mouseHookTimer = new Timer(MouseHookTimerCallback, this, Timeout.Infinite, Timeout.Infinite);
    }

    ~Win32UserInputTrigger()
    {
        if (!hHotkeyWindow.IsNull)
        {
            PInvoke.UnregisterHotKey(hHotkeyWindow, 0);
            PInvoke.DestroyWindow(hHotkeyWindow);
        }

        mouseHookHandle.Dispose();
    }

    #region HotKey

    private LRESULT HotKeyWindowProc(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == PInvoke.WM_HOTKEY)
        {
            ActionPanelRequested?.Invoke();
            return new LRESULT(1);
        }

        return PInvoke.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    #endregion

    #region Mouse Hook

    private LRESULT MouseHookProc(int code, WPARAM wParam, LPARAM lParam)
    {
        if (isProcessingMouseHook) return PInvoke.CallNextHookEx(mouseHookHandle, code, wParam, lParam);
        if (code < 0) return PInvoke.CallNextHookEx(mouseHookHandle, code, wParam, lParam);

        isProcessingMouseHook = true;
        try
        {
            ref var hookStruct = ref Unsafe.AsRef<MSLLHOOKSTRUCT>(lParam.Value.ToPointer());
            var button = hookStruct.mouseData >> 16 & 0xFFFF;

            switch (wParam.Value)
            {
                case PInvoke.WM_XBUTTONDOWN when button is PInvoke.XBUTTON1:
                {
                    pressedXButton = button;
                    mouseHookTimer.Change(TimeSpan.FromSeconds(0.5), Timeout.InfiniteTimeSpan);
                    return new LRESULT(1); // block XButton1 down event
                }
                case PInvoke.WM_XBUTTONUP when button == pressedXButton:
                {
                    pressedXButton = 0;
                    mouseHookTimer.Change(Timeout.Infinite, Timeout.Infinite);

                    if (isXButtonEventTriggered)
                    {
                        isXButtonEventTriggered = false;
                        return new LRESULT(1); // block XButton1 up event
                    }

                    // send XButton1 down and up event to the system
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
                                        mouseData = button
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
                                        mouseData = button
                                    }
                                }
                            },
                        ],
                        sizeof(INPUT));

                    break;
                }
            }

            return PInvoke.CallNextHookEx(mouseHookHandle, code, wParam, lParam);
        }
        finally
        {
            isProcessingMouseHook = false;
        }
    }

    private static void MouseHookTimerCallback(object? state)
    {
        if (state is not Win32UserInputTrigger trigger) return;

        trigger.isXButtonEventTriggered = true;
        trigger.mouseHookTimer.Change(Timeout.Infinite, Timeout.Infinite);
        trigger.ActionPanelRequested?.Invoke();
    }

    #endregion

}