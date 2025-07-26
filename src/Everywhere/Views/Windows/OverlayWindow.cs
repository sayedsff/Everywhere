using Avalonia.Controls;

namespace Everywhere.Views;

public class OverlayWindow : Window
{
    public OverlayWindow()
    {
        Topmost = true;
        CanResize = false;
        ShowInTaskbar = false;
        ShowActivated = false;
        SystemDecorations = SystemDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = null;

        var windowHelper = ServiceLocator.Resolve<INativeHelper>();
        windowHelper.SetWindowNoFocus(this);
        windowHelper.SetWindowHitTestInvisible(this);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (e.CloseReason != WindowCloseReason.WindowClosing) e.Cancel = true;
        base.OnClosing(e);
    }

    public void UpdateForVisualElement(IVisualElement? element)
    {
        if (element == null)
        {
            Hide();
            Topmost = false;
        }
        else
        {
            Show();
            Topmost = true;
            var boundingRectangle = element.BoundingRectangle;
            Position = new PixelPoint(boundingRectangle.X, boundingRectangle.Y);
            var scaling = DesktopScaling;
            Width = boundingRectangle.Width / scaling;
            Height = boundingRectangle.Height / scaling;
        }
    }
}