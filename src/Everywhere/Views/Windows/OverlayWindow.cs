using Avalonia.Controls;
using Everywhere.Common;
using Everywhere.Interop;

namespace Everywhere.Views;

public class OverlayWindow : Window
{
    private readonly WindowBase? _owner;

    public OverlayWindow(WindowBase? owner = null)
    {
        _owner = owner;

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
        }
        else
        {
            Show();
            var boundingRectangle = element.BoundingRectangle;
            Position = new PixelPoint(boundingRectangle.X, boundingRectangle.Y);
            var scaling = DesktopScaling;
            Width = boundingRectangle.Width / scaling;
            Height = boundingRectangle.Height / scaling;

            if (_owner is { Topmost: true })
            {
                _owner.Topmost = false;
                _owner.Topmost = true;
            }
        }
    }
}