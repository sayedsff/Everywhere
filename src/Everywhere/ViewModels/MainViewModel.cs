using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Lucide.Avalonia;
using Microsoft.Extensions.DependencyInjection;
using ObservableCollections;
using ShadUI.Controls;

namespace Everywhere.ViewModels;

public class MainViewModel(IServiceProvider serviceProvider) : ReactiveViewModelBase
{
    [field: AllowNull, MaybeNull]
    public NotifyCollectionChangedSynchronizedViewList<SidebarMenuItem> Pages =>
        field ??= pages.ToNotifyCollectionChangedSlim(SynchronizationContextCollectionEventDispatcher.Current);

    private readonly ObservableList<SidebarMenuItem> pages = [];

    protected internal override Task ViewLoaded(CancellationToken cancellationToken)
    {
        pages.Reset(
            serviceProvider.GetServices<IMainViewPage>().Select(
                p => new SidebarMenuItem
                {
                    [!SidebarMenuItem.HeaderProperty] = Application.Current!.Resources.GetResourceObservable(p.Title).ToBinding(),
                    Content = p,
                    Icon = new LucideIcon { Kind = p.Icon, Size = 16}
                }));
        return base.ViewLoaded(cancellationToken);
    }
}