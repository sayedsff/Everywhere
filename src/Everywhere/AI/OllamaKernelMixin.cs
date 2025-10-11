using Everywhere.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using OllamaSharp;

namespace Everywhere.AI;

/// <summary>
/// An implementation of <see cref="IKernelMixin"/> for Ollama models.
/// </summary>
public sealed class OllamaKernelMixin : KernelMixinBase
{
    public override IChatCompletionService ChatCompletionService { get; }

    public override PromptExecutionSettings GetPromptExecutionSettings(FunctionChoiceBehavior? functionChoiceBehavior = null) =>
        new OllamaPromptExecutionSettings
        {
            Temperature = (float)_settings.Temperature,
            TopP = (float)_settings.TopP,
            FunctionChoiceBehavior = functionChoiceBehavior
        };

    private readonly OllamaApiClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaKernelMixin"/> class.
    /// </summary>
    public OllamaKernelMixin(ModelSettings settings, ModelProvider provider, ModelDefinition definition) : base(settings, provider, definition)
    {
        _client = new OllamaApiClient(provider.Endpoint, definition.ModelId);
        ChatCompletionService = _client
            .To<IChatClient>()
            .AsBuilder()
            .Build()
            .AsChatCompletionService();
    }

    public override void Dispose()
    {
        _client.Dispose();
    }
}