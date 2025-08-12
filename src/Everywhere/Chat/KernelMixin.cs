using Anthropic.SDK;
using Everywhere.Enums;
using Everywhere.Models;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.TextGeneration;
using OllamaSharp;
using TextContent = Microsoft.SemanticKernel.TextContent;

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

    private class OpenAIKernelMixin(ModelSettings settings, ModelProvider provider, ModelDefinition definition) : IKernelMixin
    {
        public ITextGenerationService TextGenerationService => _chatCompletionService;

        public IChatCompletionService ChatCompletionService => _chatCompletionService;

        public PromptExecutionSettings GetPromptExecutionSettings(bool isToolRequired = false) => new OpenAIPromptExecutionSettings
        {
            Temperature = settings.Temperature,
            PresencePenalty = settings.PresencePenalty,
            FrequencyPenalty = settings.FrequencyPenalty,
            FunctionChoiceBehavior =
                isToolRequired ? FunctionChoiceBehavior.Required(autoInvoke: false) : FunctionChoiceBehavior.Auto(autoInvoke: false)
        };

        public int MaxTokenTotal => Math.Max(16_000, definition.MaxTokens);

        private readonly OpenAIChatCompletionService _chatCompletionService = new(
            definition.Id,
            new Uri(provider.Endpoint, UriKind.Absolute),
            provider.ApiKey);

        public void Dispose() { }
    }

    private class AnthropicKernelMixin : IKernelMixin, ITextGenerationService
    {
        public ITextGenerationService TextGenerationService => this;

        public IChatCompletionService ChatCompletionService { get; }

        public IReadOnlyDictionary<string, object?> Attributes => ChatCompletionService.Attributes;

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

        public async Task<IReadOnlyList<TextContent>> GetTextContentsAsync(
            string prompt,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        {
            var response = await _chatClient.GetResponseAsync(
                prompt,
                executionSettings?.ToChatOptions(kernel),
                cancellationToken: cancellationToken);
            return
            [
                new TextContent(response.Text, response.ModelId)
            ];
        }

        public async IAsyncEnumerable<StreamingTextContent> GetStreamingTextContentsAsync(
            string prompt,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var chatOptions = executionSettings?.ToChatOptions(kernel) ?? new ChatOptions();
            await foreach (var update in _chatClient.GetStreamingResponseAsync(prompt, chatOptions, cancellationToken))
            {
                yield return new StreamingTextContent(update.Text, modelId: update.ModelId);
            }
        }

        public void Dispose()
        {
            _chatClient.Dispose();
        }
    }

    private class OllamaKernelMixin : IKernelMixin
    {
        public ITextGenerationService TextGenerationService { get; }

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

            TextGenerationService = new OllamaTextGenerationService(_client);
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