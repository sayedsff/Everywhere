using Lucide.Avalonia;

namespace Everywhere.Views.Pages;

public partial class SettingsPage : ReactiveUserControl<SettingsPageViewModel>, IMainViewPage
{
    public string Title => "SettingsPage_Title";

    public LucideIconKind Icon => LucideIconKind.Cog;

    public SettingsPage()
    {
        InitializeComponent();
    }
}