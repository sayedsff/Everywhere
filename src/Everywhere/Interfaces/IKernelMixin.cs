using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.TextGeneration;

namespace Everywhere.Interfaces;

/// <summary>
/// This interface mixin Semantic Kernel and Kernel Memory services.
/// </summary>
public interface IKernelMixin : IDisposable
{
    ITextGenerationService TextGenerationService { get; }

    IChatCompletionService ChatCompletionService { get; }

    /// <summary>
    /// Gets the maximum number of tokens allowed in a single request to the model.
    /// </summary>
    int MaxTokenTotal { get; }

    PromptExecutionSettings GetPromptExecutionSettings();
}

public interface IKernelMixinFactory
{
    IKernelMixin GetOrCreate();
}