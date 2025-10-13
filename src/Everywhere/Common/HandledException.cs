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
    /// Gets the key for a localized, user-friendly error message.
    /// </summary>
    public required DynamicResourceKey FriendlyMessageKey { get; init; }

    /// <summary>
    /// Gets a value indicating whether the error is a general, non-technical error that can be shown to the user.
    /// </summary>
    public virtual bool IsExpected { get; }

    [SetsRequiredMembers]
    public HandledException(
        Exception originalException,
        DynamicResourceKey friendlyMessageKey,
        bool isExpected = true
    ) : base(originalException.Message, originalException)
    {
        FriendlyMessageKey = friendlyMessageKey;
        IsExpected = isExpected;
    }

    protected HandledException(Exception originalException) : base(originalException.Message, originalException) { }
}

/// <summary>
/// Defines the types of errors that can occur during a request to an AI kernel or service.
/// </summary>
public enum HandledChatExceptionType
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
public class HandledChatException : HandledException
{
    /// <summary>
    /// Gets a value indicating whether the error is a general, non-technical error.
    /// It is considered general unless the type is <see cref="HandledChatExceptionType.Unknown"/>.
    /// </summary>
    public override bool IsExpected => ExceptionType != HandledChatExceptionType.Unknown;

    /// <summary>
    /// Gets the categorized type of the exception.
    /// </summary>
    public HandledChatExceptionType ExceptionType { get; }

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
    public HandledChatException(
        Exception originalException,
        HandledChatExceptionType type,
        DynamicResourceKey? customFriendlyMessageKey = null
    ) : base(originalException)
    {
        ExceptionType = type;
        FriendlyMessageKey = customFriendlyMessageKey ?? new DynamicResourceKey(
            type switch
            {
                HandledChatExceptionType.InvalidConfiguration => LocaleKey.HandledChatException_InvalidConfiguration,
                HandledChatExceptionType.InvalidApiKey => LocaleKey.HandledChatException_InvalidApiKey,
                HandledChatExceptionType.QuotaExceeded => LocaleKey.HandledChatException_QuotaExceeded,
                HandledChatExceptionType.RateLimit => LocaleKey.HandledChatException_RateLimit,
                HandledChatExceptionType.EndpointNotReachable => LocaleKey.HandledChatException_EndpointNotReachable,
                HandledChatExceptionType.InvalidEndpoint => LocaleKey.HandledChatException_InvalidEndpoint,
                HandledChatExceptionType.EmptyResponse => LocaleKey.HandledChatException_EmptyResponse,
                HandledChatExceptionType.FeatureNotSupport => LocaleKey.HandledChatException_FeatureNotSupport,
                HandledChatExceptionType.Timeout => LocaleKey.HandledChatException_Timeout,
                HandledChatExceptionType.NetworkError => LocaleKey.HandledChatException_NetworkError,
                HandledChatExceptionType.ServiceUnavailable => LocaleKey.HandledChatException_ServiceUnavailable,
                HandledChatExceptionType.OperationCancelled => LocaleKey.HandledChatException_OperationCancelled,
                _ => LocaleKey.HandledChatException_Unknown,
            });
    }

    /// <summary>
    /// Parses a generic <see cref="Exception"/> into a <see cref="HandledChatException"/>.
    /// </summary>
    /// <param name="exception">The exception to parse.</param>
    /// <param name="modelProviderId">The ID of the model provider.</param>
    /// <param name="modelId">The ID of the model.</param>
    /// <returns>A new instance of <see cref="HandledChatException"/>.</returns>
    public static HandledChatException Parse(Exception exception, string? modelProviderId = null, string? modelId = null)
    {
        if (exception is HandledChatException chatRequestException) return chatRequestException;

        var context = new ExceptionParsingContext(exception);
        new ParserChain<ClientResultExceptionParser,
            ParserChain<GoogleApiExceptionParser,
                ParserChain<HttpRequestExceptionParser,
                    ParserChain<OllamaExceptionParser,
                        ParserChain<HttpOperationExceptionParser,
                            GeneralExceptionParser>>>>>().TryParse(ref context);
        new HttpStatusCodeParser().TryParse(ref context);

        return new HandledChatException(
            originalException: exception,
            type: context.ExceptionType ?? HandledChatExceptionType.Unknown)
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
    public ChatFunctionCallException(
        Exception originalException,
        DynamicResourceKey friendlyMessageKey,
        bool isGeneralError
    ) : base(originalException)
    {
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
    public HandledChatExceptionType? ExceptionType { get; set; }
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
            context.ExceptionType = HandledChatExceptionType.EmptyResponse;
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
            context.ExceptionType = HandledChatExceptionType.InvalidApiKey;
        }
        else if (message.Contains("quota", StringComparison.OrdinalIgnoreCase))
        {
            context.ExceptionType = HandledChatExceptionType.QuotaExceeded;
        }
        else if (message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            context.ExceptionType = HandledChatExceptionType.RateLimit;
        }
        else if (message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("invalid model", StringComparison.OrdinalIgnoreCase))
        {
            context.ExceptionType = HandledChatExceptionType.InvalidConfiguration;
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
                TimeoutException or TaskCanceledException { InnerException: TimeoutException } => HandledChatExceptionType.Timeout,
                SocketException => HandledChatExceptionType.NetworkError,
                _ => HandledChatExceptionType.EndpointNotReachable
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
            context.ExceptionType = HandledChatExceptionType.InvalidConfiguration;
        }
        else
        {
            context.ExceptionType = HandledChatExceptionType.Unknown;
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
            ModelDoesNotSupportToolsException => HandledChatExceptionType.FeatureNotSupport,
            AuthenticationException => HandledChatExceptionType.InvalidApiKey,
            UriFormatException => HandledChatExceptionType.InvalidEndpoint,
            OperationCanceledException => HandledChatExceptionType.OperationCancelled,
            HttpIOException => HandledChatExceptionType.NetworkError,
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

        var message = context.Exception.Message;
        context.ExceptionType = context.StatusCode switch
        {
            // 3xx Redirection
            HttpStatusCode.MultipleChoices => HandledChatExceptionType.NetworkError,
            HttpStatusCode.MovedPermanently => HandledChatExceptionType.NetworkError, // 301
            HttpStatusCode.Found => HandledChatExceptionType.NetworkError, // 302
            HttpStatusCode.SeeOther => HandledChatExceptionType.NetworkError, // 303
            HttpStatusCode.NotModified => HandledChatExceptionType.NetworkError, // 304
            HttpStatusCode.UseProxy => HandledChatExceptionType.NetworkError, // 305
            HttpStatusCode.TemporaryRedirect => HandledChatExceptionType.NetworkError, // 307
            HttpStatusCode.PermanentRedirect => HandledChatExceptionType.NetworkError, // 308

            // 4xx Client Errors
            HttpStatusCode.BadRequest => ParseException(message, HandledChatExceptionType.InvalidConfiguration), // 400
            HttpStatusCode.Unauthorized => ParseException(message, HandledChatExceptionType.InvalidApiKey), // 401
            HttpStatusCode.PaymentRequired => HandledChatExceptionType.QuotaExceeded, // 402
            HttpStatusCode.Forbidden => ParseException(message, HandledChatExceptionType.InvalidApiKey), // 403
            HttpStatusCode.NotFound => ParseException(message, HandledChatExceptionType.InvalidConfiguration), // 404
            HttpStatusCode.MethodNotAllowed => ParseException(message, HandledChatExceptionType.InvalidConfiguration), // 405
            HttpStatusCode.NotAcceptable => ParseException(message, HandledChatExceptionType.InvalidConfiguration), // 406
            HttpStatusCode.RequestTimeout => HandledChatExceptionType.Timeout, // 408
            HttpStatusCode.Conflict => ParseException(message, HandledChatExceptionType.InvalidConfiguration), // 409
            HttpStatusCode.Gone => HandledChatExceptionType.InvalidConfiguration, // 410
            HttpStatusCode.LengthRequired => HandledChatExceptionType.InvalidConfiguration, // 411
            HttpStatusCode.RequestEntityTooLarge => HandledChatExceptionType.InvalidConfiguration, // 413
            HttpStatusCode.RequestUriTooLong => HandledChatExceptionType.InvalidEndpoint, // 414
            HttpStatusCode.UnsupportedMediaType => HandledChatExceptionType.InvalidConfiguration, // 415
            HttpStatusCode.UnprocessableEntity => HandledChatExceptionType.InvalidConfiguration, // 422
            HttpStatusCode.TooManyRequests => HandledChatExceptionType.RateLimit, // 429

            // 5xx Server Errors
            HttpStatusCode.InternalServerError => ParseException(message, HandledChatExceptionType.ServiceUnavailable), // 500
            HttpStatusCode.NotImplemented => ParseException(message, HandledChatExceptionType.FeatureNotSupport), // 501
            HttpStatusCode.BadGateway => ParseException(message, HandledChatExceptionType.ServiceUnavailable), // 502
            HttpStatusCode.ServiceUnavailable => ParseException(message, HandledChatExceptionType.ServiceUnavailable), // 503
            HttpStatusCode.GatewayTimeout => HandledChatExceptionType.Timeout, // 504
            _ => null
        };
        return context.ExceptionType.HasValue;
    }

    private static HandledChatExceptionType ParseException(string message, HandledChatExceptionType fallback)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return fallback;
        }

        if (message.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("limit", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("exceeded", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("usage", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("organization", StringComparison.OrdinalIgnoreCase))
        {
            return HandledChatExceptionType.QuotaExceeded;
        }

        if (message.Contains("key", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("token", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("permission", StringComparison.OrdinalIgnoreCase))
        {
            return HandledChatExceptionType.InvalidApiKey;
        }

        if (message.Contains("model", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("parameter", StringComparison.OrdinalIgnoreCase))
        {
            return HandledChatExceptionType.InvalidConfiguration;
        }

        // Default for 403 Forbidden if no specific keywords are found
        return fallback;
    }
}

#endregion