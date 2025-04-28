namespace Everywhere.Views;

public partial class PointerActionWindow : ReactiveWindow<PointerActionWindowViewModel>
{
    public PointerActionWindow(IPlatformHandleHelper platformHandleHelper)
    {
        InitializeComponent();
        platformHandleHelper.InitializeFloatingWindow(this);
    }
}