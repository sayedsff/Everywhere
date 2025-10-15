using Anthropic.SDK;
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
            { "temperature", _customAssistant.Temperature },
            { "top_p", _customAssistant.TopP },
            { "presence_penalty", _customAssistant.PresencePenalty },
            { "frequency_penalty", _customAssistant.FrequencyPenalty },
        },
        FunctionChoiceBehavior = functionChoiceBehavior
    };

    private readonly IChatClient _chatClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnthropicKernelMixin"/> class.
    /// </summary>
    public AnthropicKernelMixin(CustomAssistant customAssistant) : base(customAssistant)
    {
        _chatClient = new AnthropicClient(new APIAuthentication(customAssistant.ApiKey))
        {
            ApiUrlFormat = customAssistant.Endpoint + "/{0}/{1}"
        }.Messages;
        ChatCompletionService = _chatClient.AsChatCompletionService();
    }

    public override void Dispose()
    {
        _chatClient.Dispose();
    }
}