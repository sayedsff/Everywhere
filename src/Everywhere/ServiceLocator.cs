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

    public static object Resolve(Type type, object? key = null)
    {
        if (serviceProvider == null) throw new InvalidOperationException($"{nameof(ServiceLocator)} is not built.");
        if (key == null) return serviceProvider.GetRequiredService(type);
        return serviceProvider.GetRequiredKeyedService(type, key);
    }

    public static T Resolve<T>(object? key = null) where T : class
    {
        return (T)Resolve(typeof(T), key);
    }
}