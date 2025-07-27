using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Models;
using Lucide.Avalonia;
using Microsoft.Extensions.DependencyInjection;
using ObservableCollections;
using ShadUI;

namespace Everywhere.ViewModels;

public partial class MainViewModel(IServiceProvider serviceProvider, Settings settings) : ReactiveViewModelBase
{
    [field: AllowNull, MaybeNull]
    public NotifyCollectionChangedSynchronizedViewList<SidebarItem> Pages =>
        field ??= pages.ToNotifyCollectionChangedSlim(SynchronizationContextCollectionEventDispatcher.Current);

    [ObservableProperty] public partial SidebarItem? SelectedPage { get; set; }

    public Settings Settings => settings;

    private readonly ObservableList<SidebarItem> pages = [];

    protected internal override Task ViewLoaded(CancellationToken cancellationToken)
    {
        pages.Reset(
            serviceProvider.GetServices<IMainViewPage>().Select(
                p => new SidebarItem
                {
                    [ContentControl.ContentProperty] = new TextBlock
                    {
                        Classes = { "p" },
                        [!TextBlock.TextProperty] = p.Title.ToBinding()
                    },
                    [SidebarItem.RouteProperty] = p,
                    Icon = new LucideIcon { Kind = p.Icon, Size = 20 }
                }));
        SelectedPage = pages.FirstOrDefault();
        return base.ViewLoaded(cancellationToken);
    }
}