using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Configuration;

namespace Everywhere.AI;

public partial class ModelDefinition : ObservableObject
{
    /// <summary>
    /// The unique identifier for the model definition.
    /// This also serves as the model ID used in API requests.
    /// e.g., "gpt-4", "gpt-3.5-turbo".
    /// </summary>
    [HiddenSettingsItem]
    public required string Id { get; init; }

    /// <summary>
    /// The model id for API calling.
    /// This is typically the same as <see cref="Id"/>, but can be customized
    /// to use a different identifier for API requests.
    /// </summary>
    [ObservableProperty]
    public required partial Customizable<string> ModelId { get; set; }

    /// <summary>
    /// Minimum version of the Everywhere application required to use this provider.
    /// If null, the provider does not have a minimum version requirement.
    /// </summary>
    [HiddenSettingsItem]
    public Version? MinimumVersion { get; set; }

    /// <summary>
    /// The display name of the model, used for UI representation.
    /// </summary>
    [ObservableProperty]
    [HiddenSettingsItem]
    public partial string? DisplayName { get; set; }

    /// <summary>
    /// Indicates whether the model supports image input capabilities.
    /// </summary>
    [ObservableProperty]
    public partial Customizable<bool> IsImageInputSupported { get; set; } = false;

    /// <summary>
    /// Indicates whether the model supports function calling capabilities.
    /// </summary>
    [ObservableProperty]
    public partial Customizable<bool> IsFunctionCallingSupported { get; set; } = false;

    /// <summary>
    /// Indicates whether the model supports tool calls.
    /// </summary>
    [ObservableProperty]
    public partial Customizable<bool> IsDeepThinkingSupported { get; set; } = false;

    /// <summary>
    /// The maximum number of tokens that the model can process in a single request.
    /// aka, the maximum context length.
    /// </summary>
    [ObservableProperty]
    [SettingsIntegerItem(IsSliderVisible = false)]
    public required partial Customizable<int> MaxTokens { get; set; }

    /// <summary>
    /// Gets or sets the default model in a model provider.
    /// This indicates the best (powerful but economical) model in the provider.
    /// </summary>
    [JsonIgnore]
    public bool IsDefault { get; set; }

    public override bool Equals(object? obj) => obj is ModelDefinition other && Id == other.Id;

    public override int GetHashCode() => Id.GetHashCode();
}