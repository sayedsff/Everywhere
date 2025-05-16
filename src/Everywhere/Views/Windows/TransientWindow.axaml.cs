using Avalonia.Controls;
using Window = ShadUI.Controls.Window;

namespace Everywhere.Views;

public partial class TransientWindow : Window
{
    public TransientWindow()
    {
        InitializeComponent();

        // determines if current system supports Mica
        if (Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 22621)
        {
            TransparencyLevelHint = [WindowTransparencyLevel.Mica];
        }
        else
        {
            TransparencyLevelHint = [WindowTransparencyLevel.AcrylicBlur ];
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        // Its content should be null before closing to make it detach from the visual tree.
        // Otherwise, it will try to attach to the visual tree again (Exception).
        Content = null;
        base.OnClosed(e);
    }
}