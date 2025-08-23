using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Enums;
using Lucide.Avalonia;
using Microsoft.SemanticKernel;
using ObservableCollections;
using ZLinq;

namespace Everywhere.Models;

[JsonPolymorphic]
[JsonDerivedType(typeof(BuiltInChatPlugin), "native")]
[JsonDerivedType(typeof(McpChatPlugin), "mcp")]
[ObservableObject]
public abstract partial class ChatPlugin(string name) : KernelPlugin(name)
{
    [JsonIgnore]
    public abstract DynamicResourceKey HeaderKey { get; }

    [JsonIgnore]
    public abstract DynamicResourceKey DescriptionKey { get; }

    [JsonIgnore]
    public virtual LucideIconKind? Icon => null;

    /// <summary>
    /// Gets the uri or svg data of the icon.
    /// </summary>
    [JsonIgnore]
    public virtual string? BeautifulIcon => null;

    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the allowed permissions for the plugin.
    /// </summary>
    [ObservableProperty]
    public partial Customizable<ChatFunctionPermissions> AllowedPermissions { get; set; } = ChatFunctionPermissions.None;

    public abstract IEnumerable<ChatFunction> Functions { get; }

    /// <summary>
    /// Gets the SettingsItems for this chat function.
    /// </summary>
    public virtual IReadOnlyList<SettingsItem>? SettingsItems => null;

    public override int FunctionCount => Functions.Count();

    public override IEnumerator<KernelFunction> GetEnumerator() =>
        Functions.Where(f => f.IsEnabled).Select(f => f.KernelFunction).GetEnumerator();

    public override bool TryGetFunction(string name, [NotNullWhen(true)] out KernelFunction? function)
    {
        function = Functions.AsValueEnumerable().Where(f => f.IsEnabled).Select(f => f.KernelFunction).FirstOrDefault(f => f.Name == name);
        return function is not null;
    }
}

/// <summary>
/// Chat kernel plugin implemented natively in Everywhere.
/// </summary>
/// <param name="name"></param>
public abstract class BuiltInChatPlugin(string name) : ChatPlugin(name)
{
    public override DynamicResourceKey HeaderKey => new($"NativeChatPlugin_{Name}_Header");

    public override DynamicResourceKey DescriptionKey => new($"NativeChatPlugin_{Name}_Description");

    public override IEnumerable<ChatFunction> Functions => _functions;

    protected readonly List<ChatFunction> _functions = [];
}

/// <summary>
/// Chat kernel plugin implemented with MCP.
/// </summary>
/// <param name="name"></param>
public partial class McpChatPlugin(string name) : ChatPlugin(name)
{
    public override DynamicResourceKey HeaderKey => new DirectResourceKey(Name);

    public override DynamicResourceKey DescriptionKey => new DirectResourceKey(Name);

    public override LucideIconKind? Icon { get; }

    /// <summary>
    /// For MCP plugins, we cannot get the permission of each function. So we use a default permission for all functions.
    /// </summary>
    [ObservableProperty]
    public partial ChatFunctionPermissions DefaultPermissions { get; set; } = ChatFunctionPermissions.AllAccess;

    public override NotifyCollectionChangedSynchronizedViewList<ChatFunction> Functions => throw new NotImplementedException();
}