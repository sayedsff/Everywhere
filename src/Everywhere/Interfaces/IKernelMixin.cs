using Microsoft.KernelMemory.AI;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.TextGeneration;

namespace Everywhere.Interfaces;

/// <summary>
/// This interface mixin Semantic Kernel and Kernel Memory services.
/// </summary>
public interface IKernelMixin : ITextGenerationService, IChatCompletionService, ITextGenerator;

public interface IKernelMixinFactory
{
    IKernelMixin Create();
}