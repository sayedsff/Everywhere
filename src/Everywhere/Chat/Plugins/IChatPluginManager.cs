using System.Diagnostics.CodeAnalysis;
using Everywhere.AI;
using ObservableCollections;

namespace Everywhere.Chat.Plugins;

/// <summary>
/// Manages chat plugins, both built-in and MCP (Modular Chat Plugin) plugins.
/// </summary>
public interface IChatPluginManager
{
    /// <summary>
    /// Gets the list of built-in chat plugins for Binding use in the UI.
    /// </summary>
    INotifyCollectionChangedSynchronizedViewList<BuiltInChatPlugin> BuiltInPlugins { get; }

    /// <summary>
    /// Gets the list of MCP chat plugins for Binding use in the UI.
    /// </summary>
    INotifyCollectionChangedSynchronizedViewList<McpChatPlugin> McpPlugins { get; }

    void AddMcpPlugin(McpTransportConfiguration configuration);

    /// <summary>
    /// Creates a new scope for available chat plugins and their functions.
    /// This method should be lightweight and fast, as it is called frequently.
    /// Functions in the scope must not have the same name.
    /// </summary>
    /// <returns></returns>
    IChatPluginScope CreateScope(ChatContext chatContext, CustomAssistant customAssistant);
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