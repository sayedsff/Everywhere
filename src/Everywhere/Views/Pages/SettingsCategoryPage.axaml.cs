using System.Reflection;
using Avalonia.Controls;
using Everywhere.Configuration;
using Lucide.Avalonia;

namespace Everywhere.Views.Pages;

/// <summary>
/// Represents a settings category page that displays a list of settings items.
/// It dynamically creates settings items based on the properties of a specified settings category.
/// </summary>
public partial class SettingsCategoryPage : UserControl, IMainViewPage
{
    public int Index { get; }

    public DynamicResourceKey Title { get; }

    public LucideIconKind Icon { get; }

    public SettingsItems Items { get; }

    public SettingsCategoryPage(int index, SettingsCategory settingsCategory)
    {
        Index = index;
        Title = new DynamicResourceKey($"SettingsCategory_{settingsCategory.Header}_Header");
        Icon = settingsCategory.Icon;
        Items = new SettingsItems(settingsCategory);

        InitializeComponent();
    }
}

public class SettingsCategoryPageFactory(Settings settings) : IMainViewPageFactory
{
    public IEnumerable<IMainViewPage> CreatePages() => typeof(Settings)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(p => p.PropertyType.IsAssignableTo(typeof(SettingsCategory)))
        .Where(p => p.GetCustomAttribute<HiddenSettingsItemAttribute>() is null)
        .Select((p, i) => new SettingsCategoryPage(i, p.GetValue(settings).NotNull<SettingsCategory>()));
}