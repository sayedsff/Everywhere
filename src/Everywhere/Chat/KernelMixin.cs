using Everywhere.Models;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Everywhere.Chat;

public class KernelMixinFactory(Settings settings) : IKernelMixinFactory
{
    private ICachedKernelMixin? cachedKernelMixin;

    public IKernelMixin Create()
    {
        if (cachedKernelMixin?.IsValid is true) return cachedKernelMixin.KernelMixin;

        cachedKernelMixin = new CachedOpenAIKernelMixin(
            settings,
            settings.Model.ModelName,
            settings.Model.Endpoint,
            settings.Model.ApiKey);
        return cachedKernelMixin.KernelMixin;
    }

    private interface ICachedKernelMixin
    {
        public bool IsValid { get; }

        public IKernelMixin KernelMixin { get; }
    }

    private class CachedOpenAIKernelMixin(Settings settings, string modelId, string endpoint, string apiKey) : ICachedKernelMixin
    {
        public bool IsValid =>
            settings.Model.ModelName == modelId &&
            settings.Model.Endpoint == endpoint &&
            settings.Model.ApiKey == apiKey;

        public IKernelMixin KernelMixin { get; } = new OpenAIKernelMixin(modelId, endpoint, apiKey);
    }
}

public class OpenAIKernelMixin(string modelId, string endpoint, string apiKey) : IKernelMixin
{
    public IReadOnlyDictionary<string, object?> Attributes => chatCompletionService.Attributes;

    public int MaxTokenTotal { get; } = modelId.ToLower() switch
    {
        "o4-mini" => 200_000,
        "o3" => 200_000,
        "gpt-4o" => 128_000,
        "gpt-4.1" => 1_047_576,
        _ => 128_000
    };

    private readonly OpenAIChatCompletionService chatCompletionService = new(modelId, new Uri(endpoint, UriKind.Absolute), apiKey);
    private readonly TiktokenTokenizer tokenizer = new(modelId);

    public Task<IReadOnlyList<TextContent>> GetTextContentsAsync(
        string prompt,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = new())
    {
        return chatCompletionService.GetTextContentsAsync(prompt, executionSettings, kernel, cancellationToken);
    }

    public IAsyncEnumerable<StreamingTextContent> GetStreamingTextContentsAsync(
        string prompt,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = new())
    {
        return chatCompletionService.GetStreamingTextContentsAsync(prompt, executionSettings, kernel, cancellationToken);
    }

    public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = new())
    {
        return chatCompletionService.GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
    }

    public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = new())
    {
        return chatCompletionService.GetStreamingChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
    }

    public int CountTokens(string text)
    {
        return tokenizer.CountTokens(text);
    }

    public IReadOnlyList<string> GetTokens(string text)
    {
        return tokenizer.GetTokens(text);
    }

    public async IAsyncEnumerable<GeneratedTextContent> GenerateTextAsync(
        string prompt,
        TextGenerationOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = new())
    {
        await foreach (var content in chatCompletionService.GetStreamingTextContentsAsync(
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
}