using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Common;
using Everywhere.Configuration;
using Everywhere.Interop;
using Everywhere.Views;
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
        if (_pages.Count > 0) return base.ViewLoaded(cancellationToken);

        _pages.Reset(
            serviceProvider
                .GetServices<IMainViewPageFactory>()
                .SelectMany(f => f.CreatePages())
                .Concat(serviceProvider.GetServices<IMainViewPage>())
                .OrderBy(p => p.Index)
                .Select(p => new SidebarItem
                {
                    [ContentControl.ContentProperty] = new TextBlock
                    {
                        [!TextBlock.TextProperty] = p.Title.ToBinding()
                    },
                    [SidebarItem.RouteProperty] = p,
                    Icon = new LucideIcon { Kind = p.Icon, Size = 20 }
                }));
        SelectedPage = _pages.FirstOrDefault();

        ShowOobeDialogOnDemand();

        return base.ViewLoaded(cancellationToken);
    }

    /// <summary>
    /// Shows the OOBE dialog if the application is launched for the first time or after an update.
    /// </summary>
    private void ShowOobeDialogOnDemand()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        if (!Version.TryParse(settings.Internal.PreviousLaunchVersion, out var previousLaunchVersion)) previousLaunchVersion = null;
        if (settings.Model.CustomAssistants.Count == 0)
        {
            DialogManager
                .CreateDialog(ServiceLocator.Resolve<WelcomeView>())
                .Show();
        }
        else if (previousLaunchVersion != version)
        {
            DialogManager
                .CreateDialog(ServiceLocator.Resolve<ChangeLogView>())
                .Dismissible()
                .Show();
        }

        settings.Internal.PreviousLaunchVersion = version?.ToString();
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