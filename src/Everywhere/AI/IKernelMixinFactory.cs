using Everywhere.Configuration;

namespace Everywhere.AI;

public interface IKernelMixinFactory
{
    IKernelMixin GetOrCreate(ModelSettings modelSettings, string? apiKeyOverride = null);
}