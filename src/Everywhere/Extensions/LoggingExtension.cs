using Microsoft.Extensions.Logging;

namespace Everywhere.Extensions;

public static class LoggingExtension
{
    public static AnonymousExceptionHandler ToExceptionHandler(this ILogger logger)
    {
        return new AnonymousExceptionHandler((exception, message, source) =>
        {
            if (message is null)
            {
                logger.LogError(exception, "[{Source}]", source);
            }
            else
            {
                logger.LogError(exception, "[{Source}] {Message}", source, message);
            }
        });
    }

    public static AnonymousExceptionHandler<T> ToExceptionHandler<T>(this ILogger<T> logger)
    {
        return new AnonymousExceptionHandler<T>((exception, message, source) =>
        {
            if (message is null)
            {
                logger.LogError(exception, "[{Source}]", source);
            }
            else
            {
                logger.LogError(exception, "[{Source}] {Message}", source, message);
            }
        });
    }
}