using Everywhere.Views.Pages;

namespace Everywhere.ViewModels;

public class AboutPageViewModel : ReactiveViewModelBase
{
    public static string Version => typeof(AboutPage).Assembly.GetName().Version?.ToString() ?? "Unknown Version";
}