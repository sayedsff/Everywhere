using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using SukiUI.Controls;

namespace Everywhere.Views;

public abstract class ReactiveUserControl<TViewModel> : UserControl where TViewModel : ReactiveViewModelBase
{
    public TViewModel ViewModel => DataContext.NotNull<TViewModel>();

    protected ReactiveUserControl()
    {
        ServiceLocator.Resolve<TViewModel>().Bind(this);
    }
}

public abstract class ReactiveWindow<TViewModel> : Window where TViewModel : ReactiveViewModelBase
{
    public TViewModel ViewModel => DataContext.NotNull<TViewModel>();

    protected ReactiveWindow()
    {
        ServiceLocator.Resolve<TViewModel>().Bind(this);
    }
}

public abstract class ReactiveSukiWindow<TViewModel> : SukiWindow where TViewModel : ReactiveViewModelBase
{
    public TViewModel ViewModel => DataContext.NotNull<TViewModel>();

    protected ReactiveSukiWindow()
    {
        ServiceLocator.Resolve<TViewModel>().Bind(this);
    }
}