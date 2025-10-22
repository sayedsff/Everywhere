using CommunityToolkit.Mvvm.Input;
using Everywhere.Common;
using Everywhere.Views;
using Everywhere.Views.Pages;
using ShadUI;

namespace Everywhere.ViewModels;

public partial class AboutPageViewModel : ReactiveViewModelBase
{
    public static string Version => typeof(AboutPage).Assembly.GetName().Version?.ToString() ?? "Unknown Version";

    [RelayCommand]
    private void OpenWelcomeDialog()
    {
        DialogManager
            .CreateDialog(ServiceLocator.Resolve<WelcomeView>())
            .ShowAsync();
    }

    [RelayCommand]
    private void OpenChangeLogDialog()
    {
        DialogManager
            .CreateDialog(ServiceLocator.Resolve<ChangeLogView>())
            .Dismissible()
            .ShowAsync();
    }
}