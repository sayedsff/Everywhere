using CommunityToolkit.Mvvm.Input;
using Everywhere.Views.Pages;
using ShadUI;

namespace Everywhere.ViewModels;

public partial class AboutPageViewModel : ReactiveViewModelBase
{
    public static string Version => typeof(AboutPage).Assembly.GetName().Version?.ToString() ?? "Unknown Version";

    [RelayCommand]
    private static void OpenWelcomeDialog()
    {
        DialogManager
            .CreateDialog(ServiceLocator.Resolve<WelcomeViewModel>())
            .Dismissible()
            .Show();
    }
}