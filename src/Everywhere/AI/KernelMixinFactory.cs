using Anthropic.SDK;
using Everywhere.Common;
using Everywhere.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OllamaSharp;

namespace Everywhere.AI;

/// <summary>
/// A factory for creating instances of <see cref="IKernelMixin"/>.
/// </summary>
public class KernelMixinFactory : IKernelMixinFactory
{
    private CachedKernelMixin? _cachedKernelMixin;

    /// <summary>
    /// Gets an existing <see cref="IKernelMixin"/> instance from the cache or creates a new one.
    /// </summary>
    /// <param name="modelSettings">Settings for the model.</param>
    /// <param name="apiKeyOverride">An optional API key to override the one in the settings.</param>
    /// <returns>A cached or new instance of <see cref="IKernelMixin"/>.</returns>
    /// <exception cref="ChatRequestException">Thrown if the model provider or definition is not found or not supported.</exception>
    public IKernelMixin GetOrCreate(ModelSettings modelSettings, string? apiKeyOverride = null)
    {
        var modelProvider = modelSettings.SelectedModelProvider;
        if (modelProvider is null)
        {
            throw new ChatRequestException(
                new InvalidOperationException("No model provider found with the selected ID."),
                KernelRequestExceptionType.InvalidConfiguration,
                new DynamicResourceKey(LocaleKey.KernelMixinFactory_NoModelProvider));
        }

        var modelDefinition = modelSettings.SelectedModelDefinition;
        if (modelDefinition is null)
        {
            throw new ChatRequestException(
                new InvalidOperationException("No model definition found with the selected ID."),
                KernelRequestExceptionType.InvalidConfiguration,
                new DynamicResourceKey(LocaleKey.KernelMixinFactory_NoModelDefinition));
        }

        var apiKey = apiKeyOverride ?? modelProvider.ApiKey;
        if (_cachedKernelMixin is not null &&
            _cachedKernelMixin.Schema == modelProvider.Schema &&
            _cachedKernelMixin.ModelId == modelDefinition.ModelId &&
            _cachedKernelMixin.Endpoint == modelProvider.Endpoint &&
            _cachedKernelMixin.ApiKey == apiKey)
        {
            return _cachedKernelMixin.KernelMixin;
        }

        _cachedKernelMixin?.KernelMixin.Dispose();
        _cachedKernelMixin = new CachedKernelMixin(
            modelProvider.Schema,
            modelDefinition.ModelId,
            modelProvider.Endpoint,
            apiKey,
            modelProvider.Schema.ActualValue switch
            {
                ModelProviderSchema.OpenAI => new OpenAIKernelMixin(modelSettings, modelProvider, modelDefinition, apiKey),
                ModelProviderSchema.Anthropic => new AnthropicKernelMixin(modelSettings, modelProvider, modelDefinition, apiKey),
                ModelProviderSchema.Ollama => new OllamaKernelMixin(modelSettings, modelProvider, modelDefinition),
                _ => throw new ChatRequestException(
                    new NotSupportedException($"Model provider schema '{modelProvider.Schema}' is not supported."),
                    KernelRequestExceptionType.InvalidConfiguration,
                    new DynamicResourceKey(LocaleKey.KernelMixinFactory_UnsupportedModelProviderSchema))
            });
        return _cachedKernelMixin.KernelMixin;
    }

    /// <summary>
    /// Represents a cached kernel mixin instance along with its configuration.
    /// </summary>
    private record CachedKernelMixin(
        ModelProviderSchema Schema,
        string ModelId,
        string Endpoint,
        string? ApiKey,
        IKernelMixin KernelMixin
    );

    /// <summary>
    /// An implementation of <see cref="IKernelMixin"/> for OpenAI models.
    /// </summary>
    private sealed class OpenAIKernelMixin(ModelSettings settings, ModelProvider provider, ModelDefinition definition, string? apiKey) : IKernelMixin
    {
        /// <inheritdoc />
        public IChatCompletionService ChatCompletionService => _chatCompletionService;

        /// <inheritdoc />
        public PromptExecutionSettings GetPromptExecutionSettings(bool isToolRequired = false, bool isToolAutoInvoke = false) =>
            new OpenAIPromptExecutionSettings
            {
                Temperature = settings.Temperature.IsCustomValueSet ? settings.Temperature.ActualValue : null,
                TopP = settings.TopP.IsCustomValueSet ? settings.TopP.ActualValue : null,
                PresencePenalty = settings.PresencePenalty.IsCustomValueSet ? settings.PresencePenalty.ActualValue : null,
                FrequencyPenalty = settings.FrequencyPenalty.IsCustomValueSet ? settings.FrequencyPenalty.ActualValue : null,
                FunctionChoiceBehavior =
                    isToolRequired ?
                        FunctionChoiceBehavior.Required(autoInvoke: isToolAutoInvoke) :
                        FunctionChoiceBehavior.Auto(autoInvoke: isToolAutoInvoke)
            };

        /// <inheritdoc />
        public int MaxTokenTotal => definition.MaxTokens;

        private readonly OpenAIChatCompletionService _chatCompletionService = new(
            definition.ModelId,
            new Uri(provider.Endpoint, UriKind.Absolute),
            apiKey);

        /// <inheritdoc />
        public void Dispose() { }
    }

    /// <summary>
    /// An implementation of <see cref="IKernelMixin"/> for Anthropic models.
    /// </summary>
    private sealed class AnthropicKernelMixin : IKernelMixin
    {
        /// <inheritdoc />
        public IChatCompletionService ChatCompletionService { get; }

        /// <inheritdoc />
        public int MaxTokenTotal => _definition.MaxTokens;

        /// <inheritdoc />
        public PromptExecutionSettings GetPromptExecutionSettings(bool isToolRequired = false, bool isToolAutoInvoke = false) => new()
        {
            ModelId = _definition.ModelId,
            ExtensionData = new Dictionary<string, object>
            {
                { "temperature", _settings.Temperature },
                { "top_p", _settings.TopP },
                { "presence_penalty", _settings.PresencePenalty },
                { "frequency_penalty", _settings.FrequencyPenalty },
            },
            FunctionChoiceBehavior =
                isToolRequired ?
                    FunctionChoiceBehavior.Required(autoInvoke: isToolAutoInvoke) :
                    FunctionChoiceBehavior.Auto(autoInvoke: isToolAutoInvoke)
        };

        private readonly ModelSettings _settings;
        private readonly ModelDefinition _definition;
        private readonly IChatClient _chatClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="AnthropicKernelMixin"/> class.
        /// </summary>
        public AnthropicKernelMixin(ModelSettings settings, ModelProvider provider, ModelDefinition definition, string? apiKey)
        {
            _settings = settings;
            _definition = definition;
            _chatClient = new AnthropicClient(new APIAuthentication(apiKey))
            {
                ApiUrlFormat = provider.Endpoint + "/{0}/{1}"
            }.Messages;
            ChatCompletionService = _chatClient.AsChatCompletionService();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _chatClient.Dispose();
        }
    }

    /// <summary>
    /// An implementation of <see cref="IKernelMixin"/> for Ollama models.
    /// </summary>
    private sealed class OllamaKernelMixin : IKernelMixin
    {
        /// <inheritdoc />
        public IChatCompletionService ChatCompletionService { get; }

        /// <inheritdoc />
        public int MaxTokenTotal => _definition.MaxTokens;

        /// <inheritdoc />
        public PromptExecutionSettings GetPromptExecutionSettings(bool isToolRequired = false, bool isToolAutoInvoke = false) =>
            new OllamaPromptExecutionSettings
            {
                Temperature = (float)_settings.Temperature,
                TopP = (float)_settings.TopP,
                FunctionChoiceBehavior =
                    isToolRequired ?
                        FunctionChoiceBehavior.Required(autoInvoke: isToolAutoInvoke) :
                        FunctionChoiceBehavior.Auto(autoInvoke: isToolAutoInvoke)
            };

        private readonly ModelSettings _settings;
        private readonly ModelDefinition _definition;
        private readonly OllamaApiClient _client;

        /// <summary>
        /// Initializes a new instance of the <see cref="OllamaKernelMixin"/> class.
        /// </summary>
        public OllamaKernelMixin(ModelSettings settings, ModelProvider provider, ModelDefinition definition)
        {
            _settings = settings;
            _definition = definition;
            _client = new OllamaApiClient(provider.Endpoint, definition.ModelId);
            ChatCompletionService = _client
                .To<IChatClient>()
                .AsBuilder()
                .Build()
                .AsChatCompletionService();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _client.Dispose();
        }
    }
}