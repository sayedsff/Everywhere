using System.Diagnostics.CodeAnalysis;
using Lucide.Avalonia;
using ObservableCollections;
using ZLinq;

namespace Everywhere.Chat.Plugins;

public class ChatPluginManager : IChatPluginManager
{
    public NotifyCollectionChangedSynchronizedViewList<BuiltInChatPlugin> BuiltInPlugins =>
        _builtInPlugins.ToNotifyCollectionChangedSlim(SynchronizationContextCollectionEventDispatcher.Current);

    public NotifyCollectionChangedSynchronizedViewList<McpChatPlugin> McpPlugins =>
        _mcpPlugins.ToNotifyCollectionChangedSlim(SynchronizationContextCollectionEventDispatcher.Current);

    private readonly ObservableList<BuiltInChatPlugin> _builtInPlugins = [];
    private readonly ObservableList<McpChatPlugin> _mcpPlugins = [];

    public ChatPluginManager(IEnumerable<BuiltInChatPlugin> builtInPlugins)
    {
        _builtInPlugins.AddRange(builtInPlugins);
    }

    public IChatPluginScope CreateScope()
    {
        return new Scope(_builtInPlugins
            .AsValueEnumerable()
            .Cast<ChatPlugin>()
            .Concat(_mcpPlugins)
            .Where(p => p.IsEnabled)
            .Select(p => new ChatPluginSnapshot(p))
            .ToList());
    }

    private class ChatPluginSnapshot(ChatPlugin original) : ChatPlugin(original.Name)
    {
        public override DynamicResourceKey HeaderKey { get; } = original.HeaderKey;
        public override DynamicResourceKey DescriptionKey { get; } = original.DescriptionKey;
        public override LucideIconKind? Icon { get; } = original.Icon;
        public override IEnumerable<ChatFunction> Functions { get; } = original.Functions.AsValueEnumerable().Where(p => p.IsEnabled).ToList();
    }

    private class Scope(List<ChatPluginSnapshot> pluginSnapshots) : IChatPluginScope
    {
        public IEnumerable<ChatPlugin> Plugins => pluginSnapshots;

        public bool TryGetPluginAndFunction(string functionName, [NotNullWhen(true)] out ChatPlugin? plugin, [NotNullWhen(true)] out ChatFunction? function)
        {
            foreach (var pluginSnapshot in pluginSnapshots)
            {
                function = pluginSnapshot.Functions.AsValueEnumerable().FirstOrDefault(f => f.KernelFunction.Name == functionName);
                if (function is not null)
                {
                    plugin = pluginSnapshot;
                    return true;
                }
            }

            plugin = null;
            function = null;
            return false;
        }
    }
}