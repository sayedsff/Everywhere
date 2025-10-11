using Everywhere.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Everywhere.AI;

/// <summary>
/// An implementation of <see cref="IKernelMixin"/> for OpenAI models.
/// </summary>
public sealed class OpenAIKernelMixin(ModelSettings settings, ModelProvider provider, ModelDefinition definition, string? apiKey)
    : KernelMixinBase(settings, provider, definition)
{
    public override IChatCompletionService ChatCompletionService => _chatCompletionService;

    public override PromptExecutionSettings? GetPromptExecutionSettings(FunctionChoiceBehavior? functionChoiceBehavior = null)
    {
        double? temperature = _settings.Temperature.IsCustomValueSet ? _settings.Temperature.ActualValue : null;
        double? topP = _settings.TopP.IsCustomValueSet ? _settings.TopP.ActualValue : null;
        double? presencePenalty = _settings.PresencePenalty.IsCustomValueSet ? _settings.PresencePenalty.ActualValue : null;
        double? frequencyPenalty = _settings.FrequencyPenalty.IsCustomValueSet ? _settings.FrequencyPenalty.ActualValue : null;

        if (temperature is null &&
            topP is null &&
            presencePenalty is null &&
            frequencyPenalty is null &&
            functionChoiceBehavior is null)
        {
            return null;
        }

        return new OpenAIPromptExecutionSettings
        {
            Temperature = temperature,
            TopP = topP,
            PresencePenalty = presencePenalty,
            FrequencyPenalty = frequencyPenalty,
            FunctionChoiceBehavior = functionChoiceBehavior
        };
    }

    private readonly OpenAIChatCompletionService _chatCompletionService = new(
        definition.ModelId,
        new Uri(provider.Endpoint, UriKind.Absolute),
        apiKey);
}