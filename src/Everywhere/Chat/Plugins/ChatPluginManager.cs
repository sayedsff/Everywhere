using System.Diagnostics.CodeAnalysis;
using Everywhere.AI;
using Everywhere.Configuration;
using Everywhere.Utilities;
using Lucide.Avalonia;
using ObservableCollections;
using ZLinq;

namespace Everywhere.Chat.Plugins;

public class ChatPluginManager : IChatPluginManager
{
    public INotifyCollectionChangedSynchronizedViewList<BuiltInChatPlugin> BuiltInPlugins =>
        _builtInPlugins.ToNotifyCollectionChangedSlim(SynchronizationContextCollectionEventDispatcher.Current);

    public INotifyCollectionChangedSynchronizedViewList<McpChatPlugin> McpPlugins =>
        _mcpPlugins.ToNotifyCollectionChangedSlim(SynchronizationContextCollectionEventDispatcher.Current);

    private readonly ObservableList<BuiltInChatPlugin> _builtInPlugins = [];
    private readonly ObservableList<McpChatPlugin> _mcpPlugins = [];

    public ChatPluginManager(IEnumerable<BuiltInChatPlugin> builtInPlugins, Settings settings)
    {
        _builtInPlugins.AddRange(builtInPlugins);

        var isEnabledRecords = settings.Plugin.IsEnabled;
        foreach (var builtInPlugin in _builtInPlugins)
        {
            builtInPlugin.IsEnabled = GetIsEnabled($"builtin.{builtInPlugin.Name}", false);
            foreach (var function in builtInPlugin.Functions)
            {
                function.IsEnabled = GetIsEnabled($"builtin.{builtInPlugin.Name}.{function.KernelFunction.Name}", true);
            }
        }

        new ObjectObserver(HandleBuiltInPluginsChange).Observe(BuiltInPlugins);


        bool GetIsEnabled(string path, bool defaultValue)
        {
            return isEnabledRecords.TryGetValue(path, out var isEnabled) ? isEnabled : defaultValue;
        }

        void HandleBuiltInPluginsChange(in ObjectObserverChangedEventArgs e)
        {
            if (!e.Path.EndsWith("IsEnabled", StringComparison.Ordinal))
            {
                return;
            }

            var parts = e.Path.Split(':');
            if (parts.Length < 2 || !int.TryParse(parts[0], out var pluginIndex) || pluginIndex < 0 || pluginIndex >= _builtInPlugins.Count)
            {
                return;
            }

            var plugin = _builtInPlugins[pluginIndex];
            switch (parts.Length)
            {
                case 2:
                {
                    settings.Plugin.IsEnabled[$"builtin.{plugin.Name}"] = plugin.IsEnabled;
                    break;
                }
                case 4 when
                    int.TryParse(parts[2], out var functionIndex) &&
                    functionIndex >= 0 &&
                    functionIndex < plugin.Functions.Count:
                {
                    var function = plugin.Functions[functionIndex];
                    settings.Plugin.IsEnabled[$"builtin.{plugin.Name}.{function.KernelFunction.Name}"] = function.IsEnabled;
                    break;
                }
            }
        }
    }

    public void AddMcpPlugin(McpTransportConfiguration configuration)
    {
        throw new NotImplementedException();
    }

    public IChatPluginScope CreateScope(ChatContext chatContext, CustomAssistant customAssistant)
    {
        // Ensure that functions in the scope do not have the same name.
        var functionNames = new HashSet<string>();
        return new ChatPluginScope(
            _builtInPlugins
                .AsValueEnumerable()
                .Cast<ChatPlugin>()
                .Concat(_mcpPlugins)
                .Where(p => p.IsEnabled)
                .Select(p => new ChatPluginSnapshot(p, chatContext, customAssistant, functionNames))
                .ToList());
    }

    private class ChatPluginScope(List<ChatPluginSnapshot> pluginSnapshots) : IChatPluginScope
    {
        public IEnumerable<ChatPlugin> Plugins => pluginSnapshots;

        public bool TryGetPluginAndFunction(
            string functionName,
            [NotNullWhen(true)] out ChatPlugin? plugin,
            [NotNullWhen(true)] out ChatFunction? function)
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

    private class ChatPluginSnapshot : ChatPlugin
    {
        public override string Key => _originalChatPlugin.Key;
        public override DynamicResourceKeyBase HeaderKey => _originalChatPlugin.HeaderKey;
        public override DynamicResourceKeyBase DescriptionKey => _originalChatPlugin.DescriptionKey;
        public override LucideIconKind? Icon => _originalChatPlugin.Icon;
        public override string? BeautifulIcon => _originalChatPlugin.BeautifulIcon;

        private readonly ChatPlugin _originalChatPlugin;

        public ChatPluginSnapshot(
            ChatPlugin originalChatPlugin,
            ChatContext chatContext,
            CustomAssistant customAssistant,
            HashSet<string> functionNames) : base(originalChatPlugin.Name)
        {
            _originalChatPlugin = originalChatPlugin;
            AllowedPermissions = originalChatPlugin.AllowedPermissions.ActualValue;
            _functions.AddRange(
                originalChatPlugin
                    .SnapshotFunctions(chatContext, customAssistant)
                    .Select(EnsureUniqueFunctionName));

            ChatFunction EnsureUniqueFunctionName(ChatFunction function)
            {
                var metadata = function.KernelFunction.Metadata;
                if (functionNames.Add(metadata.Name)) return function;

                var postfix = 1;
                string newName;
                do
                {
                    newName = $"{metadata.Name}_{postfix++}";
                }
                while (!functionNames.Add(newName));
                metadata.Name = newName;
                return function;
            }
        }
    }
}