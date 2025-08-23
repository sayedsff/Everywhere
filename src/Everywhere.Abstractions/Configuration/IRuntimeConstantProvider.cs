namespace Everywhere.Configuration;

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

    public static string EnsureWritableDataFolderPath(this IRuntimeConstantProvider provider, string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(provider.Get<string>(RuntimeConstantType.WritableDataPath), relativePath));
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetDatabasePath(this IRuntimeConstantProvider provider, string dbName)
    {
        var folderPath = provider.EnsureWritableDataFolderPath("db");
        return Path.Combine(folderPath, dbName);
    }
}