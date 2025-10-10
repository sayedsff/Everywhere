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

public class KernelMixinFactory : IKernelMixinFactory
{
    private CachedKernelMixin? _cachedKernelMixin;

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

    private record CachedKernelMixin(
        ModelProviderSchema Schema,
        string ModelId,
        string Endpoint,
        string? ApiKey,
        IKernelMixin KernelMixin
    );

    private sealed class OpenAIKernelMixin(ModelSettings settings, ModelProvider provider, ModelDefinition definition, string? apiKey) : IKernelMixin
    {
        public IChatCompletionService ChatCompletionService => _chatCompletionService;

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

        public int MaxTokenTotal => definition.MaxTokens;

        private readonly OpenAIChatCompletionService _chatCompletionService = new(
            definition.ModelId,
            new Uri(provider.Endpoint, UriKind.Absolute),
            apiKey);

        public void Dispose() { }
    }

    private sealed class AnthropicKernelMixin : IKernelMixin
    {
        public IChatCompletionService ChatCompletionService { get; }

        public int MaxTokenTotal => _definition.MaxTokens;

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

        public void Dispose()
        {
            _chatClient.Dispose();
        }
    }

    private sealed class OllamaKernelMixin : IKernelMixin
    {
        public IChatCompletionService ChatCompletionService { get; }

        public int MaxTokenTotal => _definition.MaxTokens;

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

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}