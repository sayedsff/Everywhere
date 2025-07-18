namespace Everywhere.Interfaces;

public interface IAsyncInitializer
{
    /// <summary>
    /// Larger numbers are initialized first.
    /// </summary>
    int Priority { get; }

    Task InitializeAsync();
}