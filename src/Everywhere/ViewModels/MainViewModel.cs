using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using IconPacks.Avalonia.Material;
using Microsoft.Extensions.DependencyInjection;
using ObservableCollections;
using SukiUI.Controls;

namespace Everywhere.ViewModels;

public class MainViewModel(IServiceProvider serviceProvider) : ReactiveViewModelBase
{
    [field: AllowNull, MaybeNull]
    public NotifyCollectionChangedSynchronizedViewList<SukiSideMenuItem> Pages =>
        field ??= pages.ToNotifyCollectionChangedSlim(SynchronizationContextCollectionEventDispatcher.Current);

    private readonly ObservableList<SukiSideMenuItem> pages = [];

    protected internal override Task ViewLoaded(CancellationToken cancellationToken)
    {
        pages.Reset(
            serviceProvider.GetServices<IMainViewPage>().Select(
                p => new SukiSideMenuItem
                {
                    [!SukiSideMenuItem.HeaderProperty] = Application.Current!.Resources.GetResourceObservable(p.Title).ToBinding(),
                    PageContent = p,
                    Icon = new PackIconMaterial { Width = 24, Height = 24, Kind = p.Icon },
                    IsContentMovable = false
                }));
        return base.ViewLoaded(cancellationToken);
    }
}