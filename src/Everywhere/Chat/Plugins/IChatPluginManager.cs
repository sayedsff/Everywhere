using System.Diagnostics.CodeAnalysis;
using ObservableCollections;

namespace Everywhere.Chat.Plugins;

/// <summary>
///
/// </summary>
public interface IChatPluginManager
{
    /// <summary>
    /// Gets the list of built-in chat plugins for Binding use in the UI.
    /// </summary>
    NotifyCollectionChangedSynchronizedViewList<BuiltInChatPlugin> BuiltInPlugins { get; }

    /// <summary>
    /// Gets the list of MCP chat plugins for Binding use in the UI.
    /// </summary>
    NotifyCollectionChangedSynchronizedViewList<McpChatPlugin> McpPlugins { get; }

    /// <summary>
    /// Creates a new scope for available chat plugins and their functions.
    /// This method should be lightweight and fast, as it is called frequently.
    /// </summary>
    /// <returns></returns>
    IChatPluginScope CreateScope();
}

/// <summary>
/// A scope for chat plugins, snapshot and can be used to track state during a chat session.
/// </summary>
public interface IChatPluginScope
{
    IEnumerable<ChatPlugin> Plugins { get; }

    bool TryGetPluginAndFunction(
        string functionName,
        [NotNullWhen(true)] out ChatPlugin? plugin,
        [NotNullWhen(true)] out ChatFunction? function);
}