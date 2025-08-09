using Anthropic.SDK;
using Everywhere.Enums;
using Everywhere.Models;
using Microsoft.Extensions.AI;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
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

    private class OpenAIKernelMixin(ModelSettings settings, ModelProvider provider, ModelDefinition definition) : IKernelMixin, ITextGenerator
    {
        public PromptExecutionSettings PromptExecutionSettings => new OpenAIPromptExecutionSettings
        {
            Temperature = settings.Temperature,
            TopP = settings.TopP,
            PresencePenalty = settings.PresencePenalty,
            FrequencyPenalty = settings.FrequencyPenalty,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false)
        };

        public ITextGenerationService TextGenerationService => _chatCompletionService;

        public IChatCompletionService ChatCompletionService => _chatCompletionService;

        public ITextGenerator TextGenerator => this;

        public int MaxTokenTotal => Math.Max(16_000, definition.MaxTokens);

        private readonly OpenAIChatCompletionService _chatCompletionService = new(
            definition.Id,
            new Uri(provider.Endpoint, UriKind.Absolute),
            provider.ApiKey);

        private readonly TiktokenTokenizer _tokenizer = GetTokenizer(definition.Id);

        private static TiktokenTokenizer GetTokenizer(string modelId)
        {
            try
            {
                return new TiktokenTokenizer(modelId);
            }
            catch
            {
                return new TiktokenTokenizer("gpt-4o");
            }
        }

        public int CountTokens(string text)
        {
            return _tokenizer.CountTokens(text);
        }

        public IReadOnlyList<string> GetTokens(string text)
        {
            return _tokenizer.GetTokens(text);
        }

        public async IAsyncEnumerable<GeneratedTextContent> GenerateTextAsync(
            string prompt,
            TextGenerationOptions options,
            [EnumeratorCancellation] CancellationToken cancellationToken = new())
        {
            await foreach (var content in _chatCompletionService.GetStreamingTextContentsAsync(
                               prompt,
                               new OpenAIPromptExecutionSettings
                               {
                                   Temperature = options.Temperature,
                                   TopP = options.NucleusSampling,
                                   PresencePenalty = options.PresencePenalty,
                                   FrequencyPenalty = options.FrequencyPenalty,
                                   MaxTokens = options.MaxTokens,
                                   StopSequences = options.StopSequences,
                                   TokenSelectionBiases = options.TokenSelectionBiases.ToDictionary(p => p.Key, p => (int)p.Value),
                               },
                               null,
                               cancellationToken))
            {
                if (content.Text == null) continue;
                yield return new GeneratedTextContent(content.Text);
            }
        }

        public void Dispose() { }
    }

    private class AnthropicKernelMixin : IKernelMixin, ITextGenerationService, ITextGenerator
    {
        public PromptExecutionSettings PromptExecutionSettings => new()
        {
            ModelId = _definition.Id,
            ExtensionData = new Dictionary<string, object>
            {
                { "temperature", _settings.Temperature },
                { "top_p", _settings.TopP },
                { "presence_penalty", _settings.PresencePenalty },
                { "frequency_penalty", _settings.FrequencyPenalty },
            },
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false)
        };

        public ITextGenerationService TextGenerationService => this;

        public IChatCompletionService ChatCompletionService { get; }

        public ITextGenerator TextGenerator => this;

        public IReadOnlyDictionary<string, object?> Attributes => ChatCompletionService.Attributes;

        public int MaxTokenTotal => _definition.MaxTokens;

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

        public int CountTokens(string text) => text.Length / 4;

        public IReadOnlyList<string> GetTokens(string text) => throw new NotSupportedException("Anthropic does not support tokenization.");

        public IAsyncEnumerable<GeneratedTextContent> GenerateTextAsync(
            string prompt,
            TextGenerationOptions options,
            CancellationToken cancellationToken = default)
        {
            return _chatClient.GetStreamingResponseAsync(
                prompt,
                new ChatOptions
                {
                    Temperature = (float)options.Temperature,
                    TopP = (float)options.NucleusSampling,
                    PresencePenalty = (float)options.PresencePenalty,
                    FrequencyPenalty = (float)options.FrequencyPenalty,
                    StopSequences = options.StopSequences,
                },
                cancellationToken).Select(update => new GeneratedTextContent(update.Text));
        }

        public void Dispose()
        {
            _chatClient.Dispose();
        }
    }

    private class OllamaKernelMixin : IKernelMixin, ITextGenerator
    {
        public PromptExecutionSettings PromptExecutionSettings => new OllamaPromptExecutionSettings
        {
            Temperature = (float)_settings.Temperature,
            TopP = (float)_settings.TopP,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false)
        };

        public ITextGenerationService TextGenerationService { get; }

        public IChatCompletionService ChatCompletionService { get; }

        public ITextGenerator TextGenerator => this;

        public int MaxTokenTotal => _definition.MaxTokens;

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

        public int CountTokens(string text) => text.Length / 4;

        public IReadOnlyList<string> GetTokens(string text) => throw new NotSupportedException("Ollama does not support tokenization.");

        public IAsyncEnumerable<GeneratedTextContent> GenerateTextAsync(
            string prompt,
            TextGenerationOptions options,
            CancellationToken cancellationToken = default)
        {
            return _client.GetStreamingResponseAsync(
                prompt,
                new ChatOptions
                {
                    Temperature = (float)options.Temperature,
                    TopP = (float)options.NucleusSampling,
                    PresencePenalty = (float)options.PresencePenalty,
                    FrequencyPenalty = (float)options.FrequencyPenalty,
                    StopSequences = options.StopSequences,
                },
                cancellationToken).Select(update => new GeneratedTextContent(update.Text));
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}