using System.ClientModel;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using Google;
using Microsoft.SemanticKernel;
using OllamaSharp.Models.Exceptions;

namespace Everywhere.Common;

/// <summary>
/// Represents errors that occur during application operations.
/// This is the base class for all custom exceptions in the application.
/// </summary>
public class HandledException : Exception
{
    /// <summary>
    /// Gets the original exception that caused this exception.
    /// </summary>
    public required Exception OriginalException { get; init; }

    /// <summary>
    /// Gets the key for a localized, user-friendly error message.
    /// </summary>
    public required DynamicResourceKey FriendlyMessageKey { get; init; }

    /// <summary>
    /// Gets a value indicating whether the error is a general, non-technical error that can be shown to the user.
    /// </summary>
    public virtual bool IsExpected { get; }

    /// <summary>
    /// Gets the message of the original exception.
    /// </summary>
    public override string Message => OriginalException.Message;

    protected HandledException() { }

    [SetsRequiredMembers]
    public HandledException(Exception originalException, DynamicResourceKey friendlyMessageKey, bool isExpected = true)
    {
        OriginalException = originalException;
        FriendlyMessageKey = friendlyMessageKey;
        IsExpected = isExpected;
    }
}

/// <summary>
/// Defines the types of errors that can occur during a request to an AI kernel or service.
/// </summary>
public enum KernelRequestExceptionType
{
    /// <summary>
    /// An unknown error occurred.
    /// </summary>
    Unknown,

    /// <summary>
    /// Model is not configured correctly. The model provider or ID might be missing or incorrect.
    /// </summary>
    InvalidConfiguration,

    /// <summary>
    /// API key is missing or invalid.
    /// </summary>
    InvalidApiKey,

    /// <summary>
    /// You have exceeded your API usage quota.
    /// </summary>
    QuotaExceeded,

    /// <summary>
    /// You have exceeded the request rate limit. Please try again later.
    /// </summary>
    RateLimit,

    /// <summary>
    /// Service endpoint is not reachable. Please check your network connection.
    /// </summary>
    EndpointNotReachable,

    /// <summary>
    /// Provided service endpoint is invalid.
    /// </summary>
    InvalidEndpoint,

    /// <summary>
    /// Service returned an empty response, which may indicate a network or service issue.
    /// </summary>
    EmptyResponse,

    /// <summary>
    /// Selected model does not support the requested feature.
    /// </summary>
    FeatureNotSupport,

    /// <summary>
    /// Request to the service timed out. Please try again.
    /// </summary>
    Timeout,

    /// <summary>
    /// A network error occurred. Please check your connection and try again.
    /// </summary>
    NetworkError,

    /// <summary>
    /// Service is currently unavailable. Please try again later.
    /// </summary>
    ServiceUnavailable,

    /// <summary>
    /// Operation was cancelled.
    /// </summary>
    OperationCancelled,
}

/// <summary>
/// Represents errors that occur during requests to LLM providers.
/// This class normalizes various provider-specific exceptions into a unified format.
/// </summary>
public class ChatRequestException : HandledException
{
    /// <summary>
    /// Gets a value indicating whether the error is a general, non-technical error.
    /// It is considered general unless the type is <see cref="KernelRequestExceptionType.Unknown"/>.
    /// </summary>
    public override bool IsExpected => ExceptionType != KernelRequestExceptionType.Unknown;

    /// <summary>
    /// Gets the categorized type of the exception.
    /// </summary>
    public KernelRequestExceptionType ExceptionType { get; }

    /// <summary>
    /// Gets the HTTP status code of the response, if available.
    /// </summary>
    public HttpStatusCode? StatusCode { get; init; }

    /// <summary>
    /// Gets the ID of the model provider associated with the request.
    /// </summary>
    public string? ModelProviderId { get; init; }

    /// <summary>
    /// Gets the ID of the model associated with the request.
    /// </summary>
    public string? ModelId { get; init; }

    [SetsRequiredMembers]
    public ChatRequestException(
        Exception originalException,
        KernelRequestExceptionType type,
        DynamicResourceKey? customFriendlyMessageKey = null)
    {
        OriginalException = originalException;
        ExceptionType = type;
        FriendlyMessageKey = customFriendlyMessageKey ?? new DynamicResourceKey(
            type switch
            {
                KernelRequestExceptionType.InvalidConfiguration => LocaleKey.KernelRequestException_InvalidConfiguration,
                KernelRequestExceptionType.InvalidApiKey => LocaleKey.KernelRequestException_InvalidApiKey,
                KernelRequestExceptionType.QuotaExceeded => LocaleKey.KernelRequestException_QuotaExceeded,
                KernelRequestExceptionType.RateLimit => LocaleKey.KernelRequestException_RateLimit,
                KernelRequestExceptionType.EndpointNotReachable => LocaleKey.KernelRequestException_EndpointNotReachable,
                KernelRequestExceptionType.InvalidEndpoint => LocaleKey.KernelRequestException_InvalidEndpoint,
                KernelRequestExceptionType.EmptyResponse => LocaleKey.KernelRequestException_EmptyResponse,
                KernelRequestExceptionType.FeatureNotSupport => LocaleKey.KernelRequestException_FeatureNotSupport,
                KernelRequestExceptionType.Timeout => LocaleKey.KernelRequestException_Timeout,
                KernelRequestExceptionType.NetworkError => LocaleKey.KernelRequestException_NetworkError,
                KernelRequestExceptionType.ServiceUnavailable => LocaleKey.KernelRequestException_ServiceUnavailable,
                KernelRequestExceptionType.OperationCancelled => LocaleKey.KernelRequestException_OperationCancelled,
                _ => LocaleKey.KernelRequestException_Unknown,
            });
    }

    /// <summary>
    /// Parses a generic <see cref="Exception"/> into a <see cref="ChatRequestException"/>.
    /// </summary>
    /// <param name="exception">The exception to parse.</param>
    /// <param name="modelProviderId">The ID of the model provider.</param>
    /// <param name="modelId">The ID of the model.</param>
    /// <returns>A new instance of <see cref="ChatRequestException"/>.</returns>
    public static ChatRequestException Parse(Exception exception, string? modelProviderId = null, string? modelId = null)
    {
        var context = new ExceptionParsingContext(exception);
        new ParserChain<ClientResultExceptionParser,
            ParserChain<GoogleApiExceptionParser,
                ParserChain<HttpRequestExceptionParser,
                    ParserChain<OllamaExceptionParser,
                        ParserChain<HttpOperationExceptionParser,
                            GeneralExceptionParser>>>>>().TryParse(ref context);
        new HttpStatusCodeParser().TryParse(ref context);

        return new ChatRequestException(
            originalException: exception,
            type: context.ExceptionType ?? KernelRequestExceptionType.Unknown)
        {
            StatusCode = context.StatusCode,
            ModelProviderId = modelProviderId,
            ModelId = modelId,
        };
    }
}

/// <summary>
/// Represents errors that occur during tool execution (function calling).
/// </summary>
public class ChatFunctionCallException : HandledException
{
    /// <summary>
    /// Gets a value indicating whether the error is a general, non-technical error.
    /// </summary>
    public override bool IsExpected { get; }

    [SetsRequiredMembers]
    public ChatFunctionCallException(Exception originalException, DynamicResourceKey friendlyMessageKey, bool isGeneralError)
    {
        OriginalException = originalException;
        FriendlyMessageKey = friendlyMessageKey;
        IsExpected = isGeneralError;
    }
}

#region Exception Parsers

internal readonly struct ParserChain<T1, T2> : IExceptionParser
    where T1 : struct, IExceptionParser
    where T2 : struct, IExceptionParser
{
    public bool TryParse(ref ExceptionParsingContext context)
    {
        return default(T1).TryParse(ref context) || default(T2).TryParse(ref context);
    }
}

internal ref struct ExceptionParsingContext(Exception exception)
{
    public Exception Exception { get; } = exception;
    public KernelRequestExceptionType? ExceptionType { get; set; }
    public HttpStatusCode? StatusCode { get; set; }
}

internal interface IExceptionParser
{
    bool TryParse(ref ExceptionParsingContext context);
}

internal struct ClientResultExceptionParser : IExceptionParser
{
    public bool TryParse(ref ExceptionParsingContext context)
    {
        if (context.Exception is not ClientResultException clientResult)
        {
            return false;
        }

        if (clientResult.Status == 0)
        {
            context.ExceptionType = KernelRequestExceptionType.EmptyResponse;
        }
        else
        {
            context.StatusCode = (HttpStatusCode)clientResult.Status;
        }
        return true;
    }
}

internal struct GoogleApiExceptionParser : IExceptionParser
{
    public bool TryParse(ref ExceptionParsingContext context)
    {
        if (context.Exception is not GoogleApiException googleApi)
        {
            return false;
        }

        context.StatusCode = googleApi.HttpStatusCode;
        var message = googleApi.Message;
        if (message.Contains("API key", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("permission denied", StringComparison.OrdinalIgnoreCase))
        {
            context.ExceptionType = KernelRequestExceptionType.InvalidApiKey;
        }
        else if (message.Contains("quota", StringComparison.OrdinalIgnoreCase))
        {
            context.ExceptionType = KernelRequestExceptionType.QuotaExceeded;
        }
        else if (message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            context.ExceptionType = KernelRequestExceptionType.RateLimit;
        }
        else if (message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("invalid model", StringComparison.OrdinalIgnoreCase))
        {
            context.ExceptionType = KernelRequestExceptionType.InvalidConfiguration;
        }
        return true;
    }
}

internal readonly struct HttpRequestExceptionParser : IExceptionParser
{
    public bool TryParse(ref ExceptionParsingContext context)
    {
        if (context.Exception is not HttpRequestException httpRequest)
        {
            return false;
        }

        if (httpRequest.StatusCode.HasValue)
        {
            context.StatusCode = httpRequest.StatusCode.Value;
        }
        else
        {
            context.ExceptionType = httpRequest.InnerException switch
            {
                TimeoutException or TaskCanceledException { InnerException: TimeoutException } => KernelRequestExceptionType.Timeout,
                SocketException => KernelRequestExceptionType.NetworkError,
                _ => KernelRequestExceptionType.EndpointNotReachable
            };
        }
        return true;
    }
}

internal readonly struct OllamaExceptionParser : IExceptionParser
{
    public bool TryParse(ref ExceptionParsingContext context)
    {
        if (context.Exception is not OllamaException ollama)
        {
            return false;
        }

        var message = ollama.Message;
        if (message.Contains("model", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            context.ExceptionType = KernelRequestExceptionType.InvalidConfiguration;
        }
        else
        {
            context.ExceptionType = KernelRequestExceptionType.Unknown;
        }
        return true;
    }
}

internal readonly struct HttpOperationExceptionParser : IExceptionParser
{
    public bool TryParse(ref ExceptionParsingContext context)
    {
        if (context.Exception is not HttpOperationException httpOperation)
        {
            return false;
        }
        context.StatusCode = httpOperation.StatusCode;
        return true;
    }
}

internal readonly struct GeneralExceptionParser : IExceptionParser
{
    public bool TryParse(ref ExceptionParsingContext context)
    {
        context.ExceptionType = context.Exception switch
        {
            ModelDoesNotSupportToolsException => KernelRequestExceptionType.FeatureNotSupport,
            AuthenticationException => KernelRequestExceptionType.InvalidApiKey,
            UriFormatException => KernelRequestExceptionType.InvalidEndpoint,
            OperationCanceledException => KernelRequestExceptionType.OperationCancelled,
            HttpIOException => KernelRequestExceptionType.NetworkError,
            _ => null
        };
        return context.ExceptionType.HasValue;
    }
}

internal readonly struct HttpStatusCodeParser : IExceptionParser
{
    public bool TryParse(ref ExceptionParsingContext context)
    {
        if (context.ExceptionType.HasValue || !context.StatusCode.HasValue)
        {
            return false;
        }

        context.ExceptionType = context.StatusCode switch
        {
            HttpStatusCode.BadRequest => ParseException(context.Exception.Message, KernelRequestExceptionType.InvalidConfiguration),
            HttpStatusCode.Unauthorized => KernelRequestExceptionType.InvalidApiKey,
            HttpStatusCode.PaymentRequired => KernelRequestExceptionType.QuotaExceeded,
            HttpStatusCode.Forbidden => ParseException(context.Exception.Message, KernelRequestExceptionType.InvalidApiKey),
            HttpStatusCode.NotFound => KernelRequestExceptionType.InvalidConfiguration,
            HttpStatusCode.Conflict => KernelRequestExceptionType.InvalidConfiguration,
            HttpStatusCode.UnprocessableEntity => KernelRequestExceptionType.InvalidConfiguration,
            HttpStatusCode.TooManyRequests => KernelRequestExceptionType.RateLimit,
            HttpStatusCode.RequestTimeout => KernelRequestExceptionType.Timeout,
            HttpStatusCode.InternalServerError => KernelRequestExceptionType.ServiceUnavailable,
            HttpStatusCode.BadGateway => KernelRequestExceptionType.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable => KernelRequestExceptionType.ServiceUnavailable,
            HttpStatusCode.GatewayTimeout => KernelRequestExceptionType.Timeout,
            _ => null
        };
        return context.ExceptionType.HasValue;
    }

    private static KernelRequestExceptionType ParseException(string message, KernelRequestExceptionType fallback)
    {
        if (message.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("limit", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("exceeded", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("organization", StringComparison.OrdinalIgnoreCase))
        {
            return KernelRequestExceptionType.QuotaExceeded;
        }

        if (message.Contains("key", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("authentication", StringComparison.OrdinalIgnoreCase))
        {
            return KernelRequestExceptionType.InvalidApiKey;
        }

        if (message.Contains("model", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("invalid", StringComparison.OrdinalIgnoreCase))
        {
            return KernelRequestExceptionType.InvalidConfiguration;
        }

        // Default for 403 Forbidden if no specific keywords are found
        return fallback;
    }
}

#endregion