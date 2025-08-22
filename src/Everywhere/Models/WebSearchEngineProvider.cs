using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Attributes;

namespace Everywhere.Models;

public partial class WebSearchEngineProvider : ObservableObject
{
    [HiddenSettingsItem]
    [IgnoreDataMember]
    public required string Id { get; init; }

    public required Customizable<string> EndPoint { get; init; }

    [IgnoreDataMember]
    [ObservableProperty]
    [SettingsStringItem(IsPassword = true)]
    public partial string? ApiKey { get; set; }

    [JsonIgnore]
    [HiddenSettingsItem]
    public bool IsSearchEngineIdVisible => Id.Equals("google", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// for Google search engine, this is the search engine ID.
    /// </summary>
    [IgnoreDataMember]
    [ObservableProperty]
    [SettingsItem(IsVisibleBindingPath = nameof(IsSearchEngineIdVisible))]
    public partial string? SearchEngineId { get; set; }
}