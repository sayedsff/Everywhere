using Microsoft.Extensions.DependencyInjection;

namespace Everywhere;

public static class ServiceLocator
{
    private static IServiceProvider? serviceProvider;

    public static void Build(Action<ServiceCollection> configureServices)
    {
        if (serviceProvider != null) throw new InvalidOperationException($"{nameof(ServiceLocator)} is already built.");
        var serviceCollection = new ServiceCollection();
        configureServices(serviceCollection);
        serviceProvider = serviceCollection.BuildServiceProvider();
    }

    public static T Resolve<T>(object? key = null) where T : class
    {
        if (serviceProvider == null) throw new InvalidOperationException($"{nameof(ServiceLocator)} is not built.");
        if (key == null) return serviceProvider.GetRequiredService<T>();
        return (T)serviceProvider.GetRequiredKeyedService(typeof(T), key);
    }
}