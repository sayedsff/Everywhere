using Everywhere.Models;
using Lucide.Avalonia;

namespace Everywhere.Views.Pages;

public partial class SettingsPage : ReactiveUserControl<SettingsPageViewModel>, IMainViewPage
{
    public DynamicResourceKey Title => new(LocaleKey.SettingsPage_Title);

    public LucideIconKind Icon => LucideIconKind.Cog;

    public SettingsPage()
    {
        InitializeComponent();
    }
}