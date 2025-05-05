using Microsoft.Extensions.Options;

namespace Everywhere.Interfaces;

public interface IWritableOptionsSnapshot<out TOptions> : IOptionsSnapshot<TOptions> where TOptions : class
{
    /// <summary>
    /// Write the current options to the storage.
    /// </summary>
    /// <returns></returns>
    Task WriteAsync();
}