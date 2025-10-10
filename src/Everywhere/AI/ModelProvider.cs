using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Configuration;

namespace Everywhere.AI;

/// <summary>
/// Represents a provider for models used in the Everywhere.
/// This used for both online and local models.
/// </summary>
public partial class ModelProvider : ObservableObject
{
    /// <summary>
    /// Unique identifier for the model provider.
    /// This ID is used to distinguish between different providers.
    /// </summary>
    [HiddenSettingsItem]
    public required string Id { get; init; }

    /// <summary>
    /// Minimum version of Everywhere required to use this provider.
    /// </summary>
    [HiddenSettingsItem]
    public Version? MinimumVersion { get; set; }

    /// <summary>
    /// Display name of the model provider, used for UI.
    /// This name is shown to the user in the application's settings or model selection UI.
    /// </summary>
    [ObservableProperty]
    [HiddenSettingsItem]
    public partial Customizable<string>? DisplayName { get; set; }

    /// <summary>
    /// A short description of the model provider, used for UI.
    /// Supports png, jpg, and svg image URLs.
    /// This icon is displayed next to the provider's name in the UI.
    /// </summary>
    [ObservableProperty]
    [HiddenSettingsItem]
    public partial Customizable<string>? IconUrl { get; set; }

    /// <summary>
    /// A dynamic resource key for the description of the model provider.
    /// This allows for localized descriptions that can be updated without
    /// requiring a new application build.
    /// </summary>
    [ObservableProperty]
    [HiddenSettingsItem]
    public partial Customizable<JsonDynamicResourceKey>? DescriptionKey { get; set; }

    /// <summary>
    /// Endpoint URL for the model provider's API.
    /// e.g., "https://api.example.com/v1/models".
    /// This URL is used to send requests to the model provider's servers.
    /// </summary>
    [ObservableProperty]
    public required partial Customizable<string> Endpoint { get; set; }

    /// <summary>
    /// Official website URL for the model provider, if available.
    /// This URL is displayed to the user for more information about the provider.
    /// </summary>
    [ObservableProperty]
    [HiddenSettingsItem]
    public partial Customizable<string>? OfficialWebsiteUrl { get; set; }

    /// <summary>
    /// Documentation URL for the model provider, if available.
    /// This usually points to the Everywhere's user guide or API documentation.
    /// This URL provides users with detailed information on how to use
    /// the model provider's features and API.
    /// </summary>
    [ObservableProperty]
    [HiddenSettingsItem]
    public partial Customizable<string>? DocumentationUrl { get; set; }

    /// <summary>
    /// Schema used by the model provider.
    /// This schema defines the structure of the data exchanged with the provider.
    /// </summary>
    [ObservableProperty]
    public required partial Customizable<ModelProviderSchema> Schema { get; set; }

    /// <summary>
    /// API key used to authenticate requests to the model provider.
    /// This key is required to access the model provider's API and is
    /// specific to the user's account.
    /// </summary>
    [IgnoreDataMember]
    [ObservableProperty]
    [SettingsStringItem(IsPassword = true)]
    public partial string? ApiKey { get; set; }

    /// <summary>
    /// A list of model definitions provided by this model provider.
    /// Each model definition describes a specific model offered by the provider,
    /// including its capabilities and limitations.
    /// </summary>
    [IgnoreDataMember]
    [ObservableProperty]
    public required partial ObservableCollection<ModelDefinition> ModelDefinitions { get; set; } = [];

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ModelProvider other && Id == other.Id;

    /// <inheritdoc />
    public override int GetHashCode() => Id.GetHashCode();
}