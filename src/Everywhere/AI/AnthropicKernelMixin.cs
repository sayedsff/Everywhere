using Anthropic.SDK;
using Everywhere.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Everywhere.AI;

/// <summary>
/// An implementation of <see cref="IKernelMixin"/> for Anthropic models.
/// </summary>
public sealed class AnthropicKernelMixin : KernelMixinBase
{
    public override IChatCompletionService ChatCompletionService { get; }

    public override PromptExecutionSettings GetPromptExecutionSettings(FunctionChoiceBehavior? functionChoiceBehavior = null) => new()
    {
        ModelId = ModelId,
        ExtensionData = new Dictionary<string, object>
        {
            { "temperature", _settings.Temperature },
            { "top_p", _settings.TopP },
            { "presence_penalty", _settings.PresencePenalty },
            { "frequency_penalty", _settings.FrequencyPenalty },
        },
        FunctionChoiceBehavior = functionChoiceBehavior
    };

    private readonly IChatClient _chatClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnthropicKernelMixin"/> class.
    /// </summary>
    public AnthropicKernelMixin(ModelSettings settings, ModelProvider provider, ModelDefinition definition, string? apiKey)
        : base(settings, provider, definition)
    {
        _chatClient = new AnthropicClient(new APIAuthentication(apiKey))
        {
            ApiUrlFormat = provider.Endpoint + "/{0}/{1}"
        }.Messages;
        ChatCompletionService = _chatClient.AsChatCompletionService();
    }

    public override void Dispose()
    {
        _chatClient.Dispose();
    }
}