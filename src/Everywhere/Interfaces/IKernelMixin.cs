using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Everywhere.Interfaces;

/// <summary>
/// This interface mixin Semantic Kernel and Kernel Memory services.
/// </summary>
public interface IKernelMixin : IDisposable
{
    IChatCompletionService ChatCompletionService { get; }

    /// <summary>
    /// Gets the maximum number of tokens allowed in a single request to the model.
    /// </summary>
    int MaxTokenTotal { get; }

    PromptExecutionSettings GetPromptExecutionSettings(bool isToolRequired = false);
}

public interface IKernelMixinFactory
{
    IKernelMixin GetOrCreate();
}