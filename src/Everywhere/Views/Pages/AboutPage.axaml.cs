using Lucide.Avalonia;

namespace Everywhere.Views.Pages;

public partial class AboutPage : ReactiveUserControl<AboutPageViewModel>, IMainViewPage
{
    public DynamicResourceKey Title => new(LocaleKey.AboutPage_Title);

    public LucideIconKind Icon => LucideIconKind.Info;

    public AboutPage()
    {
        InitializeComponent();
    }
}