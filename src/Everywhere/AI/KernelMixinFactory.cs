using Everywhere.Common;
using Everywhere.Configuration;

namespace Everywhere.AI;

/// <summary>
/// A factory for creating instances of <see cref="IKernelMixin"/>.
/// </summary>
public class KernelMixinFactory : IKernelMixinFactory
{
    private KernelMixinBase? _cachedKernelMixin;

    /// <summary>
    /// Gets an existing <see cref="IKernelMixin"/> instance from the cache or creates a new one.
    /// </summary>
    /// <param name="modelSettings">Settings for the model.</param>
    /// <param name="apiKeyOverride">An optional API key to override the one in the settings.</param>
    /// <returns>A cached or new instance of <see cref="IKernelMixin"/>.</returns>
    /// <exception cref="HandledChatException">Thrown if the model provider or definition is not found or not supported.</exception>
    public IKernelMixin GetOrCreate(ModelSettings modelSettings, string? apiKeyOverride = null)
    {
        var modelProvider = modelSettings.SelectedModelProvider;
        if (modelProvider is null)
        {
            throw new HandledChatException(
                new InvalidOperationException("No model provider found with the selected ID."),
                HandledChatExceptionType.InvalidConfiguration,
                new DynamicResourceKey(LocaleKey.KernelMixinFactory_NoModelProvider));
        }

        var modelDefinition = modelSettings.SelectedModelDefinition;
        if (modelDefinition is null)
        {
            throw new HandledChatException(
                new InvalidOperationException("No model definition found with the selected ID."),
                HandledChatExceptionType.InvalidConfiguration,
                new DynamicResourceKey(LocaleKey.KernelMixinFactory_NoModelDefinition));
        }

        var apiKey = apiKeyOverride ?? modelProvider.ApiKey;
        if (_cachedKernelMixin is not null &&
            _cachedKernelMixin.Schema == modelProvider.Schema &&
            _cachedKernelMixin.ModelId == modelDefinition.ModelId &&
            _cachedKernelMixin.Endpoint == modelProvider.Endpoint &&
            _cachedKernelMixin.ApiKey == apiKey)
        {
            return _cachedKernelMixin;
        }

        _cachedKernelMixin?.Dispose();
        return _cachedKernelMixin = modelProvider.Schema.ActualValue switch
        {
            ModelProviderSchema.OpenAI => new OpenAIKernelMixin(modelSettings, modelProvider, modelDefinition, apiKey),
            ModelProviderSchema.Anthropic => new AnthropicKernelMixin(modelSettings, modelProvider, modelDefinition, apiKey),
            ModelProviderSchema.Ollama => new OllamaKernelMixin(modelSettings, modelProvider, modelDefinition),
            _ => throw new HandledChatException(
                new NotSupportedException($"Model provider schema '{modelProvider.Schema}' is not supported."),
                HandledChatExceptionType.InvalidConfiguration,
                new DynamicResourceKey(LocaleKey.KernelMixinFactory_UnsupportedModelProviderSchema))
        };
    }
}