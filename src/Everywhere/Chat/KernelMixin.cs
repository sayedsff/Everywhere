using Anthropic.SDK;
using Everywhere.Enums;
using Everywhere.Models;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OllamaSharp;

namespace Everywhere.Chat;

public class KernelMixinFactory(Settings settings) : IKernelMixinFactory
{
    private CachedKernelMixin? _cachedKernelMixin;

    public IKernelMixin GetOrCreate()
    {
        var modelProvider = settings.Model.ModelProviders.FirstOrDefault(p => p.Id == settings.Model.SelectedModelProviderId);
        if (modelProvider is null)
        {
            throw new InvalidOperationException("No model provider found with the selected ID.");
        }

        var modelDefinition = modelProvider.ModelDefinitions.FirstOrDefault(m => m.Id == settings.Model.SelectedModelDefinitionId);
        if (modelDefinition is null)
        {
            throw new InvalidOperationException("No model definition found with the selected ID.");
        }

        if (_cachedKernelMixin is not null &&
            _cachedKernelMixin.Schema == modelProvider.Schema &&
            _cachedKernelMixin.ModelId == modelDefinition.Id &&
            _cachedKernelMixin.Endpoint == modelProvider.Endpoint &&
            _cachedKernelMixin.ApiKey == modelProvider.ApiKey)
        {
            return _cachedKernelMixin.KernelMixin;
        }

        _cachedKernelMixin?.KernelMixin.Dispose();
        _cachedKernelMixin = new CachedKernelMixin(
            modelProvider.Schema,
            modelDefinition.Id,
            modelProvider.Endpoint,
            modelProvider.ApiKey,
            modelProvider.Schema.ActualValue switch
            {
                ModelProviderSchema.OpenAI => new OpenAIKernelMixin(settings.Model, modelProvider, modelDefinition),
                ModelProviderSchema.Anthropic => new AnthropicKernelMixin(settings.Model, modelProvider, modelDefinition),
                ModelProviderSchema.Ollama => new OllamaKernelMixin(settings.Model, modelProvider, modelDefinition),
                _ => throw new NotSupportedException($"Model provider schema '{modelProvider.Schema}' is not supported.")
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

    private sealed class OpenAIKernelMixin(ModelSettings settings, ModelProvider provider, ModelDefinition definition) : IKernelMixin
    {
        public IChatCompletionService ChatCompletionService => _chatCompletionService;

        public PromptExecutionSettings GetPromptExecutionSettings(bool isToolRequired = false) => new OpenAIPromptExecutionSettings
        {
            Temperature = settings.Temperature.IsCustomValueSet ? settings.Temperature.ActualValue : null,
            TopP = settings.TopP.IsCustomValueSet ? settings.TopP.ActualValue : null,
            PresencePenalty = settings.PresencePenalty.IsCustomValueSet ? settings.PresencePenalty.ActualValue : null,
            FrequencyPenalty = settings.FrequencyPenalty.IsCustomValueSet ? settings.FrequencyPenalty.ActualValue : null,
            FunctionChoiceBehavior =
                isToolRequired ? FunctionChoiceBehavior.Required(autoInvoke: false) : FunctionChoiceBehavior.Auto(autoInvoke: false)
        };

        public int MaxTokenTotal => definition.MaxTokens;

        private readonly OpenAIChatCompletionService _chatCompletionService = new(
            definition.Id,
            new Uri(provider.Endpoint, UriKind.Absolute),
            provider.ApiKey);

        public void Dispose() { }
    }

    private sealed class AnthropicKernelMixin : IKernelMixin
    {
        public IChatCompletionService ChatCompletionService { get; }

        public int MaxTokenTotal => _definition.MaxTokens;

        public PromptExecutionSettings GetPromptExecutionSettings(bool isToolRequired = false) => new()
        {
            ModelId = _definition.Id,
            ExtensionData = new Dictionary<string, object>
            {
                { "temperature", _settings.Temperature },
                { "top_p", _settings.TopP },
                { "presence_penalty", _settings.PresencePenalty },
                { "frequency_penalty", _settings.FrequencyPenalty },
            },
            FunctionChoiceBehavior =
                isToolRequired ? FunctionChoiceBehavior.Required(autoInvoke: false) : FunctionChoiceBehavior.Auto(autoInvoke: false)
        };

        private readonly ModelSettings _settings;
        private readonly ModelDefinition _definition;
        private readonly IChatClient _chatClient;

        public AnthropicKernelMixin(ModelSettings settings, ModelProvider provider, ModelDefinition definition)
        {
            _settings = settings;
            _definition = definition;
            _chatClient = new AnthropicClient(new APIAuthentication(provider.ApiKey))
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

        public PromptExecutionSettings GetPromptExecutionSettings(bool isToolRequired = false) => new OllamaPromptExecutionSettings
        {
            Temperature = (float)_settings.Temperature,
            TopP = (float)_settings.TopP,
            FunctionChoiceBehavior =
                isToolRequired ? FunctionChoiceBehavior.Required(autoInvoke: false) : FunctionChoiceBehavior.Auto(autoInvoke: false)
        };

        private readonly ModelSettings _settings;
        private readonly ModelDefinition _definition;
        private readonly OllamaApiClient _client;

        public OllamaKernelMixin(ModelSettings settings, ModelProvider provider, ModelDefinition definition)
        {
            _settings = settings;
            _definition = definition;
            _client = new OllamaApiClient(provider.Endpoint, definition.Id);
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