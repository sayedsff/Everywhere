using Everywhere.Chat.Permissions;

namespace Everywhere.Chat.Plugins;

public record ChatPluginConsentRequest(
    TaskCompletionSource<ConsentDecision> Promise,
    DynamicResourceKeyBase HeaderKey,
    ChatPluginDisplayBlock? Content,
    CancellationToken CancellationToken
);

/// <summary>
/// Allows chat plugins to interact with the user interface.
/// </summary>
public interface IChatPluginUserInterface
{
    /// <summary>
    /// Requests user consent for a permission request.
    /// </summary>
    /// <remarks>
    /// Consent is grouped by plugin.function.id, so multiple calls with the same parameters will only prompt the user once (if they choose to remember their decision).
    /// </remarks>
    /// <param name="id"></param>
    /// <param name="headerKey"></param>
    /// <param name="content"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<bool> RequestConsentAsync(
        string id,
        DynamicResourceKeyBase headerKey,
        ChatPluginDisplayBlock? content = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests input from the user.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> RequestInputAsync(DynamicResourceKeyBase message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests a display sink for the plugin to output content to the user interface.
    /// </summary>
    /// <returns></returns>
    IChatPluginDisplaySink RequestDisplaySink();
}