using Avalonia.Controls;
using Avalonia.Input;

namespace Everywhere.Views;

public partial class VisualTreeDebuggerWindow : ReactiveSukiWindow<VisualTreeDebuggerWindowViewModel>
{
    private readonly nint visualElementMask;

    public VisualTreeDebuggerWindow()
    {
        InitializeComponent();

        visualElementMask = CreateWindowEx(
            WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST,
            "STATIC",
            null,
            WS_POPUP | WS_VISIBLE,
            0,
            0,
            0,
            0,
            IntPtr.Zero,
            IntPtr.Zero,
            GetModuleHandle(null),
            IntPtr.Zero
        );
        SetLayeredWindowAttributes(
            visualElementMask,
            0,
            128,
            LWA_ALPHA | LWA_COLORKEY
        );
    }

    private void HandleTreeViewPointerMoved(object? sender, PointerEventArgs e)
    {
        var element = e.Source as StyledElement;
        while (element != null)
        {
            element = element.Parent;
            if (element is TreeViewItem { DataContext: IVisualElement visualElement })
            {
                var boundingRectangle = visualElement.BoundingRectangle;
                SetWindowPos(
                    visualElementMask,
                    IntPtr.Zero,
                    boundingRectangle.X,
                    boundingRectangle.Y,
                    boundingRectangle.Width,
                    boundingRectangle.Height,
                    SWP_NOZORDER
                );
                break;
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string? lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(
        IntPtr hWnd,
        uint crKey,
        byte bAlpha,
        uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    // 窗口样式
    private const uint WS_EX_TRANSPARENT = 0x00000020;
    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_EX_TOPMOST = 0x00000008;
    private const uint WS_POPUP = 0x80000000;
    private const uint WS_VISIBLE = 0x10000000;
    private const int LWA_ALPHA = 0x00000002;
    private const int LWA_COLORKEY = 0x00000001;
    private const int SWP_NOZORDER = 0x0004;
}