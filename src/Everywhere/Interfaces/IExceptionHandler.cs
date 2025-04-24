using System.Runtime.CompilerServices;

namespace Everywhere.Interfaces;

public delegate void ExceptionHandler(Exception exception, string? message = null, [CallerMemberName] object? source = null);

public interface IExceptionHandler
{
    void HandleException(Exception exception, string? message = null, [CallerMemberName] object? source = null);
}

public class AnonymousExceptionHandler(Action<Exception, string?, object?> handler) : IExceptionHandler
{
    public void HandleException(Exception exception, string? message = null, object? source = null)
    {
        handler.Invoke(exception, message, source);
    }
}