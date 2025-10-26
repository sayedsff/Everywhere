using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.AI;
using Everywhere.Chat.Permissions;
using Everywhere.Configuration;
using Lucide.Avalonia;
using Microsoft.SemanticKernel;
using ObservableCollections;
using ZLinq;

namespace Everywhere.Chat.Plugins;

[JsonPolymorphic]
[JsonDerivedType(typeof(BuiltInChatPlugin), "builtin")]
[JsonDerivedType(typeof(McpChatPlugin), "mcp")]
[ObservableObject]
public abstract partial class ChatPlugin(string name) : KernelPlugin(name)
{
    public abstract string Key { get; }

    [JsonIgnore]
    public abstract DynamicResourceKeyBase HeaderKey { get; }

    [JsonIgnore]
    public abstract DynamicResourceKeyBase DescriptionKey { get; }

    [JsonIgnore]
    public virtual LucideIconKind? Icon => null;

    /// <summary>
    /// Gets the uri or svg data of the icon.
    /// </summary>
    [JsonIgnore]
    public virtual string? BeautifulIcon => null;

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the allowed permissions for the plugin.
    /// </summary>
    [ObservableProperty]
    public partial Customizable<ChatFunctionPermissions> AllowedPermissions { get; set; } =
        ChatFunctionPermissions.ScreenAccess |
        ChatFunctionPermissions.NetworkAccess |
        ChatFunctionPermissions.ClipboardAccess |
        ChatFunctionPermissions.FileRead;

    /// <summary>
    /// Gets the list of functions provided by this plugin for Binding use in the UI.
    /// </summary>
    [field: AllowNull, MaybeNull]
    public NotifyCollectionChangedSynchronizedViewList<ChatFunction> Functions =>
        field ??= _functions.ToNotifyCollectionChangedSlim(SynchronizationContextCollectionEventDispatcher.Current);

    /// <summary>
    /// Gets the SettingsItems for this chat function.
    /// </summary>
    public virtual IReadOnlyList<SettingsItem>? SettingsItems => null;

    public override int FunctionCount => _functions.Count(f => f.IsEnabled);

    protected readonly ObservableList<ChatFunction> _functions = [];

    public virtual IEnumerable<ChatFunction> SnapshotFunctions(ChatContext chatContext, CustomAssistant customAssistant) =>
        _functions.Where(f => f.IsEnabled);

    public override IEnumerator<KernelFunction> GetEnumerator() =>
        _functions.Where(f => f.IsEnabled).Select(f => f.KernelFunction).GetEnumerator();

    public override bool TryGetFunction(string name, [NotNullWhen(true)] out KernelFunction? function)
    {
        function = _functions.AsValueEnumerable().Where(f => f.IsEnabled).Select(f => f.KernelFunction).FirstOrDefault(f => f.Name == name);
        return function is not null;
    }
}

/// <summary>
/// Chat kernel plugin implemented natively in Everywhere.
/// </summary>
/// <param name="name"></param>
public abstract class BuiltInChatPlugin(string name) : ChatPlugin(name)
{
    public override sealed string Key => $"builtin.{Name}";
}

/// <summary>
/// Chat kernel plugin implemented with MCP.
/// </summary>
/// <param name="name"></param>
public partial class McpChatPlugin(string name) : ChatPlugin(name)
{
    public override string Key => $"mcp.{Name}";

    public override DynamicResourceKey HeaderKey => new DirectResourceKey(Name);

    public override DynamicResourceKey DescriptionKey => new DirectResourceKey(Name);

    public override LucideIconKind? Icon { get; }

    /// <summary>
    /// For MCP plugins, we cannot get the permission of each function. So we use a default permission for all functions.
    /// </summary>
    [ObservableProperty]
    public partial ChatFunctionPermissions DefaultPermissions { get; set; } = ChatFunctionPermissions.AllAccess;
}