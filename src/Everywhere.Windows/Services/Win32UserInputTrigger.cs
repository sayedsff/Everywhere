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
    public event Action? PointerActionTriggered;

    public event Action? KeyboardActionTriggered
    {
        add => TriggerImpl.HotkeyPressed += value;
        remove => TriggerImpl.HotkeyPressed -= value;
    }

    private static class TriggerImpl
    {
        #region Hotkey

        public static event Action? HotkeyPressed;

        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        // GC will not collect this delegate
        private static readonly WNDPROC LpHotKeyWndProc;

        #endregion

        #region MouseHook

        private static readonly UnhookWindowsHookExSafeHandle MouseHookHandle;
        private static readonly Timer MouseHookTimer;

        private static bool isProcessingMouseHook;
        private static uint pressedXButton;
        private static bool isXButtonEventTriggered;

        #endregion

        static TriggerImpl()
        {
            HWND hotkeyWindowHWnd;
            using var hModule = PInvoke.GetModuleHandle(null);

            // Set up the hotkey
            LpHotKeyWndProc = HotKeyWndProc;
            fixed (char* lpClassName = "Everywhere.HotKeyWindowClass")
            fixed (char* lpWindowName = "Everywhere.HotKeyWindow")
            {
                var result = PInvoke.RegisterClassEx(
                    new WNDCLASSEXW
                    {
                        cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                        lpfnWndProc = LpHotKeyWndProc,
                        hInstance = (HINSTANCE)hModule.DangerousGetHandle(),
                        lpszClassName = lpClassName
                    });
                if (result == 0)
                {
                    Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
                }

                hotkeyWindowHWnd = PInvoke.CreateWindowEx(
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
                if (hotkeyWindowHWnd.IsNull)
                {
                    Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
                }
            }

            PInvoke.RegisterHotKey(
                hotkeyWindowHWnd,
                0,
                HOT_KEY_MODIFIERS.MOD_CONTROL,
                (uint)VIRTUAL_KEY.VK_TAB
            );

            // Set up the low-level mouse hook
            MouseHookHandle = new UnhookWindowsHookExSafeHandle();
            // mouseHookHandle = PInvoke.SetWindowsHookEx(
            //     WINDOWS_HOOK_ID.WH_MOUSE_LL,
            //     MouseHookProc,
            //     PInvoke.GetModuleHandle(hModule.ModuleName),
            //     0);
            MouseHookTimer = new Timer(MouseHookTimerCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        #region HotKey

        private static LRESULT HotKeyWndProc(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam)
        {
            if (msg == PInvoke.WM_HOTKEY)
            {
                HotkeyPressed?.Invoke();
                return new LRESULT(1);
            }

            return PInvoke.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        #endregion

        #region Mouse Hook

        private static LRESULT MouseHookProc(int code, WPARAM wParam, LPARAM lParam)
        {
            if (isProcessingMouseHook) return PInvoke.CallNextHookEx(MouseHookHandle, code, wParam, lParam);
            if (code < 0) return PInvoke.CallNextHookEx(MouseHookHandle, code, wParam, lParam);

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
                        MouseHookTimer.Change(TimeSpan.FromSeconds(0.5), Timeout.InfiniteTimeSpan);
                        return new LRESULT(1); // block XButton1 down event
                    }
                    case PInvoke.WM_XBUTTONUP when button == pressedXButton:
                    {
                        pressedXButton = 0;
                        MouseHookTimer.Change(Timeout.Infinite, Timeout.Infinite);

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

                return PInvoke.CallNextHookEx(MouseHookHandle, code, wParam, lParam);
            }
            finally
            {
                isProcessingMouseHook = false;
            }
        }

        private static void MouseHookTimerCallback(object? _)
        {
            isXButtonEventTriggered = true;
            MouseHookTimer.Change(Timeout.Infinite, Timeout.Infinite);
            // PointerActionTriggered?.Invoke();
        }

        #endregion

    }
}