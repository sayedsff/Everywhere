namespace Everywhere.AI;

/// <summary>
/// Represents a factory for creating instances of <see cref="IKernelMixin"/>.
/// </summary>
public interface IKernelMixinFactory
{
    IKernelMixin GetOrCreate(CustomAssistant customAssistant, string? apiKeyOverride = null);
}