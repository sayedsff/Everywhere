using System.Net;
using System.Net.Sockets;
using Microsoft.SemanticKernel;

namespace Everywhere.Extensions;

public static class ExceptionExtension
{
    /// <summary>
    /// 将Exception转换为用户友好的消息
    /// </summary>
    /// <returns></returns>
    public static DynamicResourceKey GetFriendlyMessage(this Exception e)
    {
        switch (e)
        {
            case HttpRequestException hre:
            {
                return FormatHttpExceptionMessage(LocaleKey.FriendlyExceptionMessage_HttpRequest, hre.StatusCode);
            }
            case HttpOperationException hoe:
            {
                return FormatHttpExceptionMessage(LocaleKey.FriendlyExceptionMessage_HttpRequest, hoe.StatusCode);
            }
            case SocketException se:
            {
                return new FormattedDynamicResourceKey(
                    LocaleKey.FriendlyExceptionMessage_Socket,
                    new DirectResourceKey((int)se.SocketErrorCode),
                    new DynamicResourceKey($"{LocaleKey.FriendlyExceptionMessage_Socket}_{se.SocketErrorCode.ToString()}"));
            }
            case AggregateException ae:
            {
                return new FormattedDynamicResourceKey(
                    LocaleKey.FriendlyExceptionMessage_Aggregate,
                    new AggregateDynamicResourceKey(ae.InnerExceptions.Select(DynamicResourceKeyBase (i) => i.GetFriendlyMessage()).ToArray()));
            }
            default:
            {
                var exceptionName = e.GetType().Name;
                if (exceptionName.EndsWith("Exception")) exceptionName = exceptionName[..^"Exception".Length];
                return new FormattedDynamicResourceKey(
                    $"FriendlyExceptionMessage_{exceptionName}",
                    new DirectResourceKey(e.Message));
            }
        }
    }

    private static FormattedDynamicResourceKey FormatHttpExceptionMessage(string baseKey, HttpStatusCode? statusCode)
    {
        var key = $"{baseKey}_{statusCode.ToString()}";
        if (!DynamicResourceKey.Exists(key)) key = $"{baseKey}_0";
        return new FormattedDynamicResourceKey(
            baseKey,
            new DirectResourceKey(statusCode ?? 0),
            new DynamicResourceKey(key));
    }
}