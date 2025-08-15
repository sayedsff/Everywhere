using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Attributes;
using Everywhere.Enums;

namespace Everywhere.Models;

/// <summary>
/// Represents a provider for models used in the Everywhere application.
/// This used for both online and local models.
/// </summary>
public partial class ModelProvider : ObservableObject
{
    /// <summary>
    /// The unique identifier for the model provider.
    /// </summary>
    [HiddenSettingsItem]
    public required string Id { get; init; }

    /// <summary>
    /// Minimum version of the Everywhere application required to use this provider.
    /// If null, the provider does not have a minimum version requirement.
    /// </summary>
    [HiddenSettingsItem]
    public Version? MinimumVersion { get; set; }

    /// <summary>
    /// The display name of the model provider, used for UI representation.
    /// </summary>
    [ObservableProperty]
    [HiddenSettingsItem]
    public partial Customizable<string>? DisplayName { get; set; }

    /// <summary>
    /// A short description of the model provider, used for UI representation.
    /// Supports png, jpg, and svg image URLs.
    /// </summary>
    [ObservableProperty]
    [HiddenSettingsItem]
    public partial Customizable<string>? IconUrl { get; set; }

    /// <summary>
    /// A dynamic resource key for the description of the model provider.
    /// </summary>
    [ObservableProperty]
    [HiddenSettingsItem]
    public partial Customizable<JsonDynamicResourceKey>? DescriptionKey { get; set; }

    /// <summary>
    /// The endpoint URL for the model provider's API.
    /// e.g., "https://api.example.com/v1/models".
    /// </summary>
    [ObservableProperty]
    public required partial Customizable<string> Endpoint { get; set; }

    /// <summary>
    /// The official website URL for the model provider, if available.
    /// </summary>
    [ObservableProperty]
    [HiddenSettingsItem]
    public partial Customizable<string>? OfficialWebsiteUrl { get; set; }

    /// <summary>
    /// The documentation URL for the model provider, if available.
    /// This usually points to the Everywhere's user guide or API documentation.
    /// </summary>
    [ObservableProperty]
    [HiddenSettingsItem]
    public partial Customizable<string>? DocumentationUrl { get; set; }

    /// <summary>
    /// The schema used by the model provider.
    /// </summary>
    [ObservableProperty]
    public required partial Customizable<ModelProviderSchema> Schema { get; set; }

    /// <summary>
    /// The API key used to authenticate requests to the model provider.
    /// </summary>
    [IgnoreDataMember]
    [ObservableProperty]
    [SettingsStringItem(IsPassword = true)]
    public partial string? ApiKey { get; set; }

    /// <summary>
    /// A list of model definitions provided by this model provider.
    /// </summary>
    [IgnoreDataMember]
    [ObservableProperty]
    public required partial ObservableCollection<ModelDefinition> ModelDefinitions { get; set; } = [];

    public override bool Equals(object? obj) => obj is ModelProvider other && Id == other.Id;

    public override int GetHashCode() => Id.GetHashCode();
}