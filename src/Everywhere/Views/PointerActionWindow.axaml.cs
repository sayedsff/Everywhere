using Avalonia.Interactivity;

namespace Everywhere.Views;

public partial class PointerActionWindow : ReactiveWindow<PointerActionWindowViewModel>
{
    public PointerActionWindow()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
    }
}