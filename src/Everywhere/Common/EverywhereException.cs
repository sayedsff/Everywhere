using System.ClientModel;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Authentication;
using Google;
using Microsoft.SemanticKernel;
using OllamaSharp.Models.Exceptions;

namespace Everywhere.Common;

/// <summary>
/// Represents errors that occur during everywhere operations.
/// </summary>
public abstract class EverywhereException : Exception
{
    public required Exception OriginalException { get; init; }

    public required DynamicResourceKey FriendlyMessageKey { get; init; }

    /// <summary>
    /// User visible but non-technical error message.
    /// </summary>
    public abstract bool IsGeneralError { get; }

    public override string Message => OriginalException.Message;
}

public enum KernelRequestExceptionType
{
    /// <summary>
    /// Unknown error.
    /// </summary>
    Unknown,

    /// <summary>
    /// model is not configured properly. e.g. model provider or id is missing or invalid.
    /// </summary>
    InvalidConfiguration,

    /// <summary>
    /// API key is missing or invalid.
    /// </summary>
    InvalidApiKey,

    /// <summary>
    /// Quota exceeded.
    /// </summary>
    QuotaExceeded,

    /// <summary>
    /// Rate limit exceeded.
    /// </summary>
    RateLimit,

    /// <summary>
    /// Endpoint is not reachable.
    /// </summary>
    EndpointNotReachable,

    /// <summary>
    /// Endpoint is invalid.
    /// </summary>
    InvalidEndpoint,

    /// <summary>
    /// The response from the model provider is empty, this may indicate a network issue.
    /// </summary>
    EmptyResponse,

    /// <summary>
    /// The model does not support the requested feature,
    /// </summary>
    FeatureNotSupport,

    /// <summary>
    /// The request to the model provider timed out.
    /// </summary>
    Timeout,

    /// <summary>
    /// A network error occurred.
    /// </summary>
    NetworkError,

    /// <summary>
    /// The model provider service is currently error or unavailable.
    /// </summary>
    ServiceUnavailable,
}

/// <summary>
/// Represents errors that occur during requests to LLM providers.
/// </summary>
/// <remarks>
/// LLM providers may throw their own errors, which are not wrapped by this exception.
/// These errors typically only specify a vague HTTP error code and use string prompts to indicate the error,
/// making them difficult to track and localize.
/// This class is responsible for parsing and representing them.
/// </remarks>
public class ChatRequestException : EverywhereException
{
    public override bool IsGeneralError => ExceptionType != KernelRequestExceptionType.Unknown;

    public KernelRequestExceptionType ExceptionType { get; }

    public HttpStatusCode? StatusCode { get; init; }

    public string? ModelProviderId { get; init; }

    public string? ModelId { get; init; }

    [SetsRequiredMembers]
    public ChatRequestException(
        Exception originalException,
        KernelRequestExceptionType type,
        DynamicResourceKey? customFriendlyMessageKey = null)
    {
        OriginalException = originalException;
        ExceptionType = type;
        FriendlyMessageKey = customFriendlyMessageKey ?? new DynamicResourceKey(type switch
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
            _ => LocaleKey.KernelRequestException_Unknown,
        });
    }

    public static ChatRequestException Parse(Exception exception, string? modelProviderId = null, string? modelId = null)
    {
        KernelRequestExceptionType? exceptionType = null;
        HttpStatusCode? httpStatusCode = null;

        switch (exception)
        {
            case HttpOperationException httpOperation:
            {
                httpStatusCode = httpOperation.StatusCode;
                break;
            }
            case ClientResultException { Status: 0 }:
            {
                exceptionType = KernelRequestExceptionType.EmptyResponse;
                break;
            }
            case ClientResultException clientResult:
            {
                httpStatusCode = (HttpStatusCode)clientResult.Status;
                break;
            }
            case GoogleApiException googleApi:
            {
                httpStatusCode = googleApi.HttpStatusCode;
                var message = googleApi.Message;
                if (message.Contains("API key", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("credential", StringComparison.OrdinalIgnoreCase))
                {
                    exceptionType = KernelRequestExceptionType.InvalidApiKey;
                }
                else if (message.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
                         message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
                {
                    exceptionType = KernelRequestExceptionType.QuotaExceeded;
                }
                else if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    exceptionType = KernelRequestExceptionType.InvalidConfiguration;
                }
                break;
            }
            case ModelDoesNotSupportToolsException:
            {
                exceptionType = KernelRequestExceptionType.FeatureNotSupport;
                break;
            }
            case OllamaException ollama:
            {
                exceptionType = ollama.Message.Contains("model", StringComparison.OrdinalIgnoreCase) ?
                    KernelRequestExceptionType.InvalidConfiguration :
                    KernelRequestExceptionType.Unknown;
                break;
            }
            case HttpRequestException httpRequest:
            {
                if (httpRequest.StatusCode.HasValue)
                {
                    httpStatusCode = httpRequest.StatusCode.Value;
                }
                else
                {
                    exceptionType = httpRequest.InnerException switch
                    {
                        TimeoutException => KernelRequestExceptionType.Timeout,
                        _ => KernelRequestExceptionType.NetworkError
                    };
                }

                break;
            }
            case HttpIOException:
            {
                exceptionType = KernelRequestExceptionType.NetworkError;
                break;
            }
            case AuthenticationException:
            {
                exceptionType = KernelRequestExceptionType.InvalidApiKey;
                break;
            }
            case UriFormatException:
            {
                exceptionType = KernelRequestExceptionType.InvalidEndpoint;
                break;
            }
        }

        exceptionType ??= httpStatusCode switch
        {
            HttpStatusCode.BadRequest => KernelRequestExceptionType.InvalidConfiguration,
            HttpStatusCode.Unauthorized => KernelRequestExceptionType.InvalidApiKey,
            HttpStatusCode.Forbidden => KernelRequestExceptionType.QuotaExceeded,
            HttpStatusCode.NotFound => KernelRequestExceptionType.InvalidConfiguration,
            HttpStatusCode.RequestTimeout => KernelRequestExceptionType.Timeout,
            HttpStatusCode.InternalServerError => KernelRequestExceptionType.ServiceUnavailable,
            HttpStatusCode.BadGateway => KernelRequestExceptionType.ServiceUnavailable,
            HttpStatusCode.GatewayTimeout => KernelRequestExceptionType.Timeout,
            HttpStatusCode.TooManyRequests => KernelRequestExceptionType.RateLimit,
            _ => KernelRequestExceptionType.Unknown,
        };

        return new ChatRequestException(
            originalException: exception,
            type: exceptionType.Value)
        {
            StatusCode = httpStatusCode,
            ModelProviderId = modelProviderId,
            ModelId = modelId,
        };
    }
}

/// <summary>
/// Represents errors that occur during tool execution.
/// </summary>
public class ChatFunctionCallException : EverywhereException
{
    /// <summary>
    /// User visible but non-technical error message.
    /// </summary>
    /// <example>
    /// ToolNotFound, ToolExecutionFailed, etc.
    /// </example>
    /// <returns></returns>
    public override bool IsGeneralError { get; }

    [SetsRequiredMembers]
    public ChatFunctionCallException(Exception originalException, DynamicResourceKey friendlyMessageKey, bool isGeneralError)
    {
        OriginalException = originalException;
        FriendlyMessageKey = friendlyMessageKey;
        IsGeneralError = isGeneralError;
    }
}