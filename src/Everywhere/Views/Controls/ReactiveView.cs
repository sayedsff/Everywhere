using Avalonia.Controls;
using Everywhere.Common;

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

public abstract class ReactiveShadWindow<TViewModel> : ShadUI.Window where TViewModel : ReactiveViewModelBase
{
    public TViewModel ViewModel { get; }

    protected ReactiveShadWindow()
    {
        ViewModel = ServiceLocator.Resolve<TViewModel>();
        ViewModel.Bind(this);
    }
}