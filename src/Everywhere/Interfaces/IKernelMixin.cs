using Microsoft.KernelMemory.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.TextGeneration;

namespace Everywhere.Interfaces;

/// <summary>
/// This interface mixin Semantic Kernel and Kernel Memory services.
/// </summary>
public interface IKernelMixin : IDisposable
{
    PromptExecutionSettings PromptExecutionSettings { get; }

    ITextGenerationService TextGenerationService { get; }

    IChatCompletionService ChatCompletionService { get; }

    ITextGenerator TextGenerator { get; }
}

public interface IKernelMixinFactory
{
    IKernelMixin GetOrCreate();
}