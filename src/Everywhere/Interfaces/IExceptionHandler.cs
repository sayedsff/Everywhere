namespace Everywhere.Interfaces;

public interface IExceptionHandler
{
    void HandleException(Exception exception, string? message = null, [CallerMemberName] object? source = null);

    public static IExceptionHandler DangerouslyIgnoreAllException { get; } = new AnonymousExceptionHandler(static (_, _, _) => { });
}

/// <summary>
/// Generic interface for exception handlers that holds a type parameter.
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IExceptionHandler<out T> : IExceptionHandler;

public readonly struct AnonymousExceptionHandler(Action<Exception, string?, object?> handler) : IExceptionHandler
{
    public void HandleException(Exception exception, string? message = null, object? source = null)
    {
        handler.Invoke(exception, message, source);
    }
}

public readonly struct AnonymousExceptionHandler<T>(Action<Exception, string?, object?> handler) : IExceptionHandler<T>
{
    public void HandleException(Exception exception, string? message = null, object? source = null)
    {
        handler.Invoke(exception, message, $"{nameof(T)}.{source}");
    }
}

public readonly ref struct AnonymousExceptionHandlerSlim(Action<Exception, string?, object?> handler) : IExceptionHandler
{
    public void HandleException(Exception exception, string? message = null, object? source = null)
    {
        handler.Invoke(exception, message, source);
    }
}