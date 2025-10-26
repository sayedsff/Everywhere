using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Chat.Plugins;

namespace Everywhere.Configuration;

public partial class WebSearchEngineSettings : SettingsCategory
{
    [ObservableProperty]
    public partial ObservableCollection<WebSearchEngineProvider> WebSearchEngineProviders { get; set; } = [];

    [HiddenSettingsItem]
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedWebSearchEngineProvider))]
    public partial string? SelectedWebSearchEngineProviderId { get; set; }

    [JsonIgnore]
    [SettingsItems(IsExpanded = true)]
    [SettingsSelectionItem(
        ItemsSourceBindingPath = nameof(WebSearchEngineProviders),
        DataTemplateKey = typeof(WebSearchEngineProvider))]
    public WebSearchEngineProvider? SelectedWebSearchEngineProvider
    {
        get => WebSearchEngineProviders.FirstOrDefault(p => p.Id == SelectedWebSearchEngineProviderId);
        set
        {
            if (Equals(SelectedWebSearchEngineProviderId, value?.Id)) return;
            SelectedWebSearchEngineProviderId = value?.Id;
        }
    }
}