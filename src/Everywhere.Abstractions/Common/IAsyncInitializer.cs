namespace Everywhere.Common;

public enum AsyncInitializerPriority
{
    Database = 10,

    Settings = 100,
    AfterSettings = 101,

    Startup = int.MaxValue,
}

public interface IAsyncInitializer
{
    /// <summary>
    /// Smaller numbers are initialized first.
    /// </summary>
    AsyncInitializerPriority Priority { get; }

    Task InitializeAsync();
}