using System.Diagnostics.CodeAnalysis;
using System.Reflection;
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
        field ??= _pages.ToNotifyCollectionChangedSlim(SynchronizationContextCollectionEventDispatcher.Current);

    [ObservableProperty] public partial SidebarItem? SelectedPage { get; set; }

    public Settings Settings => settings;

    private readonly ObservableList<SidebarItem> _pages = [];

    protected internal override Task ViewLoaded(CancellationToken cancellationToken)
    {
        _pages.Reset(
            serviceProvider.GetServices<IMainViewPage>().Select(p => new SidebarItem
            {
                [ContentControl.ContentProperty] = new TextBlock
                {
                    Classes = { "p" },
                    [!TextBlock.TextProperty] = p.Title.ToBinding()
                },
                [SidebarItem.RouteProperty] = p,
                Icon = new LucideIcon { Kind = p.Icon, Size = 20 }
            }));
        SelectedPage = _pages.FirstOrDefault();

        ShowWelcomeDialogOnDemand();

        return base.ViewLoaded(cancellationToken);
    }

    /// <summary>
    /// Shows the welcome dialog if the application is launched for the first time or after an update.
    /// </summary>
    private void ShowWelcomeDialogOnDemand()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
        if (settings.Internal.PreviousLaunchVersion == version) return;

        DialogManager
            .CreateDialog(ServiceLocator.Resolve<WelcomeViewModel>())
            .Dismissible()
            .Show();

        settings.Internal.PreviousLaunchVersion = version;
    }

    protected internal override Task ViewUnloaded()
    {
        ShowHideToTrayNotificationOnDemand();

        return base.ViewUnloaded();
    }

    private void ShowHideToTrayNotificationOnDemand()
    {
        if (!settings.Internal.IsFirstTimeHideToTrayIcon) return;

        ServiceLocator.Resolve<INativeHelper>().ShowDesktopNotification(LocaleKey.MainView_EverywhereHasMinimizedToTray.I18N());
        settings.Internal.IsFirstTimeHideToTrayIcon = false;
    }
}