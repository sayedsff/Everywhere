using Avalonia.Controls;
using SukiUI.Controls;

namespace Everywhere.Views;

public abstract class ReactiveUserControl<TViewModel> : UserControl where TViewModel : ReactiveViewModelBase
{
    public TViewModel ViewModel { get; }

    protected ReactiveUserControl()
    {
        ViewModel = ServiceLocator.Resolve<TViewModel>();
        ViewModel.Bind(this);
    }
}

public abstract class ReactiveWindow<TViewModel> : Window where TViewModel : ReactiveViewModelBase
{
    public TViewModel ViewModel { get; }

    protected ReactiveWindow()
    {
        ViewModel = ServiceLocator.Resolve<TViewModel>();
        ViewModel.Bind(this);
    }
}

public abstract class ReactiveSukiWindow<TViewModel> : SukiWindow where TViewModel : ReactiveViewModelBase
{
    public TViewModel ViewModel { get; }

    protected ReactiveSukiWindow()
    {
        ViewModel = ServiceLocator.Resolve<TViewModel>();
        ViewModel.Bind(this);
    }
}