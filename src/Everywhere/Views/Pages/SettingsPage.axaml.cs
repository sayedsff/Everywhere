using IconPacks.Avalonia.Material;

namespace Everywhere.Views.Pages;

public partial class SettingsPage : ReactiveUserControl<SettingsPageViewModel>, IMainViewPage
{
    public string Title => "SettingsPage_Title";

    public PackIconMaterialKind Icon => PackIconMaterialKind.Cog;

    public SettingsPage()
    {
        InitializeComponent();
    }
}