using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace Everywhere.Views;

public partial class VisualTreeDebuggerWindow : ReactiveSukiWindow<VisualTreeDebuggerWindowViewModel>
{
    private readonly Window treeViewFocusMask;

    public VisualTreeDebuggerWindow(IVisualElementContext visualElementContext)
    {
        InitializeComponent();

        treeViewFocusMask = new Window
        {
            Topmost = true,
            IsHitTestVisible = false,
            ShowInTaskbar = false,
            ShowActivated = false,
            SystemDecorations = SystemDecorations.None,
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
            Background = null,
            Content = new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.DodgerBlue,
                Opacity = 0.5
            }
        };
        EnableClickThrough(treeViewFocusMask);

        var keyboardFocusMask = new Window
        {
            Topmost = true,
            IsHitTestVisible = false,
            ShowInTaskbar = false,
            ShowActivated = false,
            SystemDecorations = SystemDecorations.None,
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
            Background = null,
            Content = new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Crimson,
                Opacity = 0.5
            }
        };
        EnableClickThrough(keyboardFocusMask);

        visualElementContext.KeyboardFocusedElementChanged += element =>
        {
            Dispatcher.UIThread.Invoke(() => SetMask(keyboardFocusMask, element));
        };
    }

    private void HandleTreeViewPointerMoved(object? sender, PointerEventArgs e)
    {
        var element = e.Source as StyledElement;
        while (element != null)
        {
            element = element.Parent;
            if (element is TreeViewItem { DataContext: IVisualElement visualElement })
            {
                SetMask(treeViewFocusMask, visualElement);
                return;
            }
        }

        SetMask(treeViewFocusMask, null);
    }

    private static void SetMask(Window mask, IVisualElement? element)
    {
        if (element == null)
        {
            mask.Hide();
        }
        else
        {
            mask.Show();
            var boundingRectangle = element.BoundingRectangle;
            mask.Position = new PixelPoint(boundingRectangle.X, boundingRectangle.Y);
            mask.Width = boundingRectangle.Width / mask.DesktopScaling;
            mask.Height = boundingRectangle.Height / mask.DesktopScaling;
        }
    }

    private const int GWL_EXSTYLE   = -20;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const uint LWA_ALPHA    = 0x2;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int index, int newStyle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint flags);

    public static void EnableClickThrough(Window window)
    {
        window.Loaded += delegate
        {
            if (window.TryGetPlatformHandle() is not { } handle) return;
            var hWnd = handle.Handle;
            var style = GetWindowLong(hWnd, GWL_EXSTYLE);
            SetWindowLong(hWnd, GWL_EXSTYLE, style | WS_EX_LAYERED | WS_EX_TRANSPARENT);
            SetLayeredWindowAttributes(hWnd, 0, 255, LWA_ALPHA);
        };
    }
}