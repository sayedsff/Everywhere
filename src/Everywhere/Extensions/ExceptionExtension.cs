using System.Net.Sockets;
using Everywhere.Models;

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
                var statusCode = hre.StatusCode ?? 0;
                return new FormattedDynamicResourceKey(
                    LocaleKey.FriendlyExceptionMessage_HttpRequest,
                    new DirectResourceKey((int)statusCode),
                    new DynamicResourceKey($"{LocaleKey.FriendlyExceptionMessage_HttpRequest}_{statusCode.ToString()}"));
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
}