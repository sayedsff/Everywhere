using Everywhere.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Everywhere.AI;

public abstract class KernelMixinBase(ModelSettings settings, ModelProvider provider, ModelDefinition definition) : IKernelMixin
{
    // cache properties for comparison
    public ModelProviderSchema Schema { get; } = provider.Schema;
    public string Endpoint { get; } = provider.Endpoint;
    public string? ApiKey { get; } = provider.ApiKey;
    public string ModelId { get; } = definition.ModelId;

    public int ContextWindow => definition.MaxTokens;
    public bool IsImageInputSupported => definition.IsImageInputSupported;
    public bool IsFunctionCallingSupported => definition.IsFunctionCallingSupported;
    public bool IsDeepThinkingSupported => definition.IsDeepThinkingSupported;

    public abstract IChatCompletionService ChatCompletionService { get; }

    /// <summary>
    /// WARNING: properties are mutable!
    /// </summary>
    protected readonly ModelSettings _settings = settings;

    /// <summary>
    /// indicates whether the model is reasoning
    /// </summary>
    protected static readonly AdditionalPropertiesDictionary ReasoningProperties = new()
    {
        ["reasoning"] = true
    };

    protected static AdditionalPropertiesDictionary ApplyReasoningProperties(AdditionalPropertiesDictionary? dictionary)
    {
        if (dictionary is null) return ReasoningProperties;
        dictionary["reasoning"] = true;
        return dictionary;
    }

    public abstract PromptExecutionSettings? GetPromptExecutionSettings(FunctionChoiceBehavior? functionChoiceBehavior = null);

    public virtual void Dispose() { }
}