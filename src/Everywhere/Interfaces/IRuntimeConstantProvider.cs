using Everywhere.Enums;

namespace Everywhere.Interfaces;

public interface IRuntimeConstantProvider
{
    object? this[RuntimeConstantType type] { get; }
}

public static class RuntimeConstantProviderExtensions
{
    public static T Get<T>(this IRuntimeConstantProvider provider, RuntimeConstantType type) where T : class
    {
        return provider[type].NotNull<T>();
    }

    public static string GetDatabasePath(this IRuntimeConstantProvider provider, string dbName)
    {
        var folderPath = Path.Combine(provider.Get<string>(RuntimeConstantType.WritableDataPath), "db");
        Directory.CreateDirectory(folderPath);
        return Path.Combine(folderPath, dbName);
    }
}