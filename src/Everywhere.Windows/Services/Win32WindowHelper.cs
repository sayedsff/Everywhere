using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;
using Avalonia;
using Avalonia.Controls;
using Everywhere.Interfaces;

namespace Everywhere.Windows.Services;

public class Win32WindowHelper : IWindowHelper
{
    // ReSharper disable InconsistentNaming
    // ReSharper disable IdentifierTypo
    // ReSharper disable UnusedMember.Local
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;

    private enum WINDOW_MESSAGE
    {
        WM_NULL = 0x0000,
        WM_CREATE = 0x0001,
        WM_DESTROY = 0x0002,
        WM_MOVE = 0x0003,
        WM_SIZE = 0x0005,
        WM_ACTIVATE = 0x0006,
        WM_SETFOCUS = 0x0007,
        WM_KILLFOCUS = 0x0008,
        WM_ENABLE = 0x000A,
        WM_SETREDRAW = 0x000B,
        WM_SETTEXT = 0x000C,
        WM_GETTEXT = 0x000D,
        WM_GETTEXTLENGTH = 0x000E,
        WM_PAINT = 0x000F,
        WM_CLOSE = 0x0010,
        WM_QUERYENDSESSION = 0x0011,
        WM_QUERYOPEN = 0x0013,
        WM_ENDSESSION = 0x0016,
        WM_QUIT = 0x0012,
        WM_ERASEBKGND = 0x0014,
        WM_SYSCOLORCHANGE = 0x0015,
        WM_SHOWWINDOW = 0x0018,
        WM_WININICHANGE = 0x001A,
        WM_SETTINGCHANGE = 0x001A,
        WM_DEVMODECHANGE = 0x001B,
        WM_ACTIVATEAPP = 0x001C,
        WM_FONTCHANGE = 0x001D,
        WM_TIMECHANGE = 0x001E,
        WM_CANCELMODE = 0x001F,
        WM_SETCURSOR = 0x0020,
        WM_MOUSEACTIVATE = 0x0021,
        WM_CHILDACTIVATE = 0x0022,
        WM_QUEUESYNC = 0x0023,
        WM_GETMINMAXINFO = 0x0024,
        WM_PAINTICON = 0x0026,
        WM_ICONERASEBKGND = 0x0027,
        WM_NEXTDLGCTL = 0x0028,
        WM_SPOOLERSTATUS = 0x002A,
        WM_DRAWITEM = 0x002B,
        WM_MEASUREITEM = 0x002C,
        WM_DELETEITEM = 0x002D,
        WM_VKEYTOITEM = 0x002E,
        WM_CHARTOITEM = 0x002F,
        WM_SETFONT = 0x0030,
        WM_GETFONT = 0x0031,
        WM_SETHOTKEY = 0x0032,
        WM_GETHOTKEY = 0x0033,
        WM_QUERYDRAGICON = 0x0037,
        WM_COMPAREITEM = 0x0039,
        WM_GETOBJECT = 0x003D,
        WM_COMPACTING = 0x0041,
        WM_COMMNOTIFY = 0x0044,
        WM_WINDOWPOSCHANGING = 0x0046,
        WM_WINDOWPOSCHANGED = 0x0047,
        WM_POWER = 0x0048,
        WM_COPYDATA = 0x004A,
        WM_CANCELJOURNAL = 0x004B,
        WM_NOTIFY = 0x004E,
        WM_INPUTLANGCHANGEREQUEST = 0x0050,
        WM_INPUTLANGCHANGE = 0x0051,
        WM_TCARD = 0x0052,
        WM_HELP = 0x0053,
        WM_USERCHANGED = 0x0054,
        WM_NOTIFYFORMAT = 0x0055,
        WM_CONTEXTMENU = 0x007B,
        WM_STYLECHANGING = 0x007C,
        WM_STYLECHANGED = 0x007D,
        WM_DISPLAYCHANGE = 0x007E,
        WM_GETICON = 0x007F,
        WM_SETICON = 0x0080,
        WM_NCCREATE = 0x0081,
        WM_NCDESTROY = 0x0082,
        WM_NCCALCSIZE = 0x0083,
        WM_NCHITTEST = 0x0084,
        WM_NCPAINT = 0x0085,
        WM_NCACTIVATE = 0x0086,
        WM_GETDLGCODE = 0x0087,
        WM_SYNCPAINT = 0x0088,
        WM_NCMOUSEMOVE = 0x00A0,
        WM_NCLBUTTONDOWN = 0x00A1,
        WM_NCLBUTTONUP = 0x00A2,
        WM_NCLBUTTONDBLCLK = 0x00A3,
        WM_NCRBUTTONDOWN = 0x00A4,
        WM_NCRBUTTONUP = 0x00A5,
        WM_NCRBUTTONDBLCLK = 0x00A6,
        WM_NCMBUTTONDOWN = 0x00A7,
        WM_NCMBUTTONUP = 0x00A8,
        WM_NCMBUTTONDBLCLK = 0x00A9,
        WM_NCXBUTTONDOWN = 0x00AB,
        WM_NCXBUTTONUP = 0x00AC,
        WM_NCXBUTTONDBLCLK = 0x00AD,
        WM_INPUT = 0x00FF,
        WM_KEYFIRST = 0x0100,
        WM_KEYDOWN = 0x0100,
        WM_KEYUP = 0x0101,
        WM_CHAR = 0x0102,
        WM_DEADCHAR = 0x0103,
        WM_SYSKEYDOWN = 0x0104,
        WM_SYSKEYUP = 0x0105,
        WM_SYSCHAR = 0x0106,
        WM_SYSDEADCHAR = 0x0107,
        WM_KEYLAST = 0x0109,
        WM_UNICHAR = 0x0109,
        WM_IME_STARTCOMPOSITION = 0x010D,
        WM_IME_ENDCOMPOSITION = 0x010E,
        WM_IME_COMPOSITION = 0x010F,
        WM_IME_KEYLAST = 0x010F,
        WM_INITDIALOG = 0x0110,
        WM_COMMAND = 0x0111,
        WM_SYSCOMMAND = 0x0112,
        WM_TIMER = 0x0113,
        WM_HSCROLL = 0x0114,
        WM_VSCROLL = 0x0115,
        WM_INITMENU = 0x0116,
        WM_INITMENUPOPUP = 0x0117,
        WM_MENUSELECT = 0x011F,
        WM_MENUCHAR = 0x0120,
        WM_ENTERIDLE = 0x0121,
        WM_MENURBUTTONUP = 0x0122,
        WM_MENUDRAG = 0x0123,
        WM_MENUGETOBJECT = 0x0124,
        WM_UNINITMENUPOPUP = 0x0125,
        WM_MENUCOMMAND = 0x0126,
        WM_CHANGEUISTATE = 0x0127,
        WM_UPDATEUISTATE = 0x0128,
        WM_QUERYUISTATE = 0x0129,
        WM_CTLCOLORMSGBOX = 0x0132,
        WM_CTLCOLOREDIT = 0x0133,
        WM_CTLCOLORLISTBOX = 0x0134,
        WM_CTLCOLORBTN = 0x0135,
        WM_CTLCOLORDLG = 0x0136,
        WM_CTLCOLORSCROLLBAR = 0x0137,
        WM_CTLCOLORSTATIC = 0x0138,
        WM_MOUSEFIRST = 0x0200,
        WM_MOUSEMOVE = 0x0200,
        WM_LBUTTONDOWN = 0x0201,
        WM_LBUTTONUP = 0x0202,
        WM_LBUTTONDBLCLK = 0x0203,
        WM_RBUTTONDOWN = 0x0204,
        WM_RBUTTONUP = 0x0205,
        WM_RBUTTONDBLCLK = 0x0206,
        WM_MBUTTONDOWN = 0x0207,
        WM_MBUTTONUP = 0x0208,
        WM_MBUTTONDBLCLK = 0x0209,
        //WM_MOUSELAST(95) = 0x0209,
        WM_MOUSEWHEEL = 0x020A,
        //WM_MOUSELAST(NT4,98) = 0x020A,
        WM_XBUTTONDOWN = 0x020B,
        WM_XBUTTONUP = 0x020C,
        WM_XBUTTONDBLCLK = 0x020D,
        //WM_MOUSELAST(2K,XP,2k3) = 0x020D,
        WM_PARENTNOTIFY = 0x0210,
        WM_ENTERMENULOOP = 0x0211,
        WM_EXITMENULOOP = 0x0212,
        WM_NEXTMENU = 0x0213,
        WM_SIZING = 0x0214,
        WM_CAPTURECHANGED = 0x0215,
        WM_MOVING = 0x0216,
        WM_POWERBROADCAST = 0x0218,
        WM_DEVICECHANGE = 0x0219,
        WM_MDICREATE = 0x0220,
        WM_MDIDESTROY = 0x0221,
        WM_MDIACTIVATE = 0x0222,
        WM_MDIRESTORE = 0x0223,
        WM_MDINEXT = 0x0224,
        WM_MDIMAXIMIZE = 0x0225,
        WM_MDITILE = 0x0226,
        WM_MDICASCADE = 0x0227,
        WM_MDIICONARRANGE = 0x0228,
        WM_MDIGETACTIVE = 0x0229,
        WM_MDISETMENU = 0x0230,
        WM_ENTERSIZEMOVE = 0x0231,
        WM_EXITSIZEMOVE = 0x0232,
        WM_DROPFILES = 0x0233,
        WM_MDIREFRESHMENU = 0x0234,
        WM_IME_SETCONTEXT = 0x0281,
        WM_IME_NOTIFY = 0x0282,
        WM_IME_CONTROL = 0x0283,
        WM_IME_COMPOSITIONFULL = 0x0284,
        WM_IME_SELECT = 0x0285,
        WM_IME_CHAR = 0x0286,
        WM_IME_REQUEST = 0x0288,
        WM_IME_KEYDOWN = 0x0290,
        WM_IME_KEYUP = 0x0291,
        WM_MOUSEHOVER = 0x02A1,
        WM_MOUSELEAVE = 0x02A3,
        WM_NCMOUSEHOVER = 0x02A0,
        WM_NCMOUSELEAVE = 0x02A2,
        WM_WTSSESSION_CHANGE = 0x02B1,
        WM_TABLET_FIRST = 0x02C0,
        WM_TABLET_LAST = 0x02DF,
        WM_CUT = 0x0300,
        WM_COPY = 0x0301,
        WM_PASTE = 0x0302,
        WM_CLEAR = 0x0303,
        WM_UNDO = 0x0304,
        WM_RENDERFORMAT = 0x0305,
        WM_RENDERALLFORMATS = 0x0306,
        WM_DESTROYCLIPBOARD = 0x0307,
        WM_DRAWCLIPBOARD = 0x0308,
        WM_PAINTCLIPBOARD = 0x0309,
        WM_VSCROLLCLIPBOARD = 0x030A,
        WM_SIZECLIPBOARD = 0x030B,
        WM_ASKCBFORMATNAME = 0x030C,
        WM_CHANGECBCHAIN = 0x030D,
        WM_HSCROLLCLIPBOARD = 0x030E,
        WM_QUERYNEWPALETTE = 0x030F,
        WM_PALETTEISCHANGING = 0x0310,
        WM_PALETTECHANGED = 0x0311,
        WM_HOTKEY = 0x0312,
        WM_PRINT = 0x0317,
        WM_PRINTCLIENT = 0x0318,
        WM_APPCOMMAND = 0x0319,
        WM_THEMECHANGED = 0x031A,
        WM_HANDHELDFIRST = 0x0358,
        WM_HANDHELDLAST = 0x035F,
        WM_AFXFIRST = 0x0360,
        WM_AFXLAST = 0x037F,
        WM_PENWINFIRST = 0x0380,
        WM_PENWINLAST = 0x038F,
        WM_USER = 0x0400,
        WM_APP = 0x8000
    }
    // ReSharper restore InconsistentNaming
    // ReSharper restore IdentifierTypo
    // ReSharper restore UnusedMember.Local

    public unsafe void SetWindowNoFocus(Window window)
    {
        Win32Properties.AddWindowStylesCallback(window, WindowStylesCallback);

        static (uint style, uint exStyle) WindowStylesCallback(uint style, uint exStyle)
        {
            return (style, exStyle |
                (uint)WINDOW_EX_STYLE.WS_EX_NOACTIVATE |
                (uint)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW |
                (uint)WINDOW_EX_STYLE.WS_EX_TOPMOST);
        }

        Win32Properties.AddWndProcHookCallback(window, WndProcHookCallback);

        IntPtr WndProcHookCallback(IntPtr hWnd, uint msg, IntPtr wparam, IntPtr lparam, ref bool handled)
        {
            // handle and block all activate messages
            // if (msg is not (>= (uint)WINDOW_MESSAGE.WM_MOUSEMOVE and <= (uint)WINDOW_MESSAGE.WM_XBUTTONDBLCLK or (uint)WINDOW_MESSAGE.WM_NCHITTEST))
            //     Console.WriteLine($"{(WINDOW_MESSAGE)msg}\t{wparam}\t{lparam}");
            switch (msg)
            {
                case (uint)WINDOW_MESSAGE.WM_MOUSEACTIVATE:
                    handled = true;
                    return 3; // MA_NOACTIVATE;
                case (uint)WINDOW_MESSAGE.WM_ACTIVATE:
                case (uint)WINDOW_MESSAGE.WM_SETFOCUS:
                case (uint)WINDOW_MESSAGE.WM_KILLFOCUS:
                case (uint)WINDOW_MESSAGE.WM_ACTIVATEAPP:
                case (uint)WINDOW_MESSAGE.WM_NCACTIVATE:
                    handled = true;
                    return IntPtr.Zero;
                default:
                    return IntPtr.Zero;
            }
        }

        // TODO: Following is broken
        uint tid = 0, targetTid = 0;

        window.GotFocus += (_, e) =>
        {
            if (e.Source is not TextBox) return;
            tid = PInvoke.GetCurrentThreadId();
            var targetHWnd = PInvoke.GetForegroundWindow();
            targetTid = PInvoke.GetWindowThreadProcessId(targetHWnd, null);
            PInvoke.AttachThreadInput(targetTid, tid, true);
        };

        window.LostFocus += (_, e) =>
        {
            if (e.Source is not TextBox) return;
            PInvoke.AttachThreadInput(targetTid, tid, false);
            tid = targetTid = 0;
        };

        window.PropertyChanged += (_, e) =>
        {
            if (e.Property != Visual.IsVisibleProperty || e.NewValue is not false) return;
#pragma warning disable CS0618 // 类型或成员已过时
            window.FocusManager?.ClearFocus(); // why, avalonia, why!!!!!!!!!!!!!!!!!!
#pragma warning restore CS0618 // 类型或成员已过时
            PInvoke.AttachThreadInput(targetTid, tid, false);
            tid = targetTid = 0;
        };
    }

    public void SetWindowAutoHide(Window window)
    {
        var thisHWnd = window.TryGetPlatformHandle()?.Handle ?? 0;
        if (thisHWnd == 0)
        {
            throw new InvalidOperationException("Failed to get platform handle for the top-level window.");
        }

        var targetHWnd = HWND.Null;
        window.Loaded += delegate
        {
            targetHWnd = PInvoke.GetForegroundWindow();
            if (targetHWnd == 0)
            {
                throw new InvalidOperationException("Failed to get platform handle for the target window.");
            }
        };
        window.Unloaded += delegate
        {
            targetHWnd = HWND.Null;
        };

        var lpWinEventProc = new WINEVENTPROC(WinEventProc);
        var handle = GCHandle.Alloc(lpWinEventProc);
        var winEventHook = PInvoke.SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND,
            EVENT_SYSTEM_FOREGROUND,
            HMODULE.Null,
            lpWinEventProc,
            0,
            0,
            WINEVENT_OUTOFCONTEXT);
        window.Closed += delegate
        {
            PInvoke.UnhookWinEvent(winEventHook);
            handle.Free();
        };

        void WinEventProc(
            HWINEVENTHOOK hWinEventHook,
            uint eventType,
            HWND hWnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime)
        {
            var foregroundWindow = PInvoke.GetForegroundWindow();
            if (foregroundWindow != targetHWnd && foregroundWindow != thisHWnd)
            {
                window.IsVisible = false;
            }
        }
    }

    public void SetWindowHitTestInvisible(Window window)
    {
        window.Loaded += delegate
        {
            if (window.TryGetPlatformHandle() is not { } handle) return;
            var hWnd = (HWND)handle.Handle;
            var style = PInvoke.GetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
            _ = PInvoke.SetWindowLong(
                hWnd,
                WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
                style | (int)WINDOW_EX_STYLE.WS_EX_LAYERED | (int)WINDOW_EX_STYLE.WS_EX_TRANSPARENT);
            PInvoke.SetLayeredWindowAttributes(hWnd, new COLORREF(), 255, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);
        };
    }

    public void SetWindowCornerRadius(Window window, CornerRadius cornerRadius)
    {
        // TODO
        // https://github.com/selastingeorge/Win32-Acrylic-Effect
        // https://gist.github.com/ADeltaX/aea6aac248604d0cb7d423a61b06e247
        var hWnd = window.TryGetPlatformHandle()?.Handle ?? 0;
        if (hWnd == 0) return;

        var bounds = window.Bounds;
        var scale = window.DesktopScaling;
        var (w, h) = ((int)(bounds.Width * scale), (int)(bounds.Height * scale));
        if (w <= 0 || h <= 0) return;

        var (rTL, rTR, rBR, rBL) =
            ((int)(cornerRadius.TopLeft * scale),
                (int)(cornerRadius.TopRight * scale),
                (int)(cornerRadius.BottomRight * scale),
                (int)(cornerRadius.BottomLeft * scale));

        using var rgnTL = PInvoke.CreateRoundRectRgn_SafeHandle(0, 0, rTL * 2, rTL * 2, rTL * 2, rTL * 2);
        using var rgnTR = PInvoke.CreateRoundRectRgn_SafeHandle(w - rTR * 2, 0, w, rTR * 2, rTR * 2, rTR * 2);
        using var rgnBR = PInvoke.CreateRoundRectRgn_SafeHandle(w - rBR * 2, h - rBR * 2, w, h, rBR * 2, rBR * 2);
        using var rgnBL = PInvoke.CreateRoundRectRgn_SafeHandle(0, h - rBL * 2, rBL * 2, h, rBL * 2, rBL * 2);

        // using var rgnCenter = PInvoke.CreateRectRgn_SafeHandle(rTL, 0, w - rTR, h);
        // using var rgnMiddle = PInvoke.CreateRectRgn_SafeHandle(0, rTL, w, h - rBL);

        using var finalRgn = PInvoke.CreateRectRgn_SafeHandle(0, 0, 0, 0);
        // PInvoke.CombineRgn(finalRgn, rgnCenter, rgnTL, RGN_COMBINE_MODE.RGN_OR);
        PInvoke.CombineRgn(finalRgn, finalRgn, rgnTR, RGN_COMBINE_MODE.RGN_OR);
        PInvoke.CombineRgn(finalRgn, finalRgn, rgnBR, RGN_COMBINE_MODE.RGN_OR);
        PInvoke.CombineRgn(finalRgn, finalRgn, rgnBL, RGN_COMBINE_MODE.RGN_OR);
        // PInvoke.CombineRgn(finalRgn, finalRgn, rgnMiddle, RGN_COMBINE_MODE.RGN_OR);

        PInvoke.SetWindowRgn((HWND)hWnd, (HRGN)finalRgn.DangerousGetHandle(), true);
    }
}