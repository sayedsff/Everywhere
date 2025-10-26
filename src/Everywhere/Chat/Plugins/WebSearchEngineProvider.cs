using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Configuration;

namespace Everywhere.Chat.Plugins;

public partial class WebSearchEngineProvider : ObservableObject
{
    [HiddenSettingsItem]
    [IgnoreDataMember]
    public required string Id { get; init; }

    [JsonIgnore]
    [HiddenSettingsItem]
    public string DisplayName { get; init; } = string.Empty;
    
    public required Customizable<string> EndPoint { get; init; }

    [IgnoreDataMember]
    [ObservableProperty]
    [SettingsItem(IsVisibleBindingPath = nameof(IsApiKeyRequired))]
    [SettingsStringItem(IsPassword = true)]
    public partial string? ApiKey { get; set; }

    [JsonIgnore]
    [HiddenSettingsItem]
    public bool IsSearchEngineIdVisible => Id.Equals("google", StringComparison.OrdinalIgnoreCase);
    
    [JsonIgnore]
    [HiddenSettingsItem]
    public bool IsApiKeyRequired => !Id.Equals("searxng", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// for Google search engine, this is the search engine ID.
    /// </summary>
    [IgnoreDataMember]
    [ObservableProperty]
    [SettingsItem(IsVisibleBindingPath = nameof(IsSearchEngineIdVisible))]
    public partial string? SearchEngineId { get; set; }
}