using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Configuration;
using Lucide.Avalonia;
using Microsoft.SemanticKernel;

namespace Everywhere.Chat.Plugins;

public abstract partial class ChatFunction : ObservableObject
{
    public LucideIconKind? Icon { get; set; }

    /// <summary>
    /// The permissions required by this function.
    /// </summary>
    public virtual ChatFunctionPermissions Permissions => ChatFunctionPermissions.AllAccess;

    /// <summary>
    /// The permissions currently granted to this function.
    /// </summary>
    [ObservableProperty]
    public partial Customizable<ChatFunctionPermissions> GrantedPermissions { get; set; } = ChatFunctionPermissions.None;

    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = true;

    public abstract KernelFunction KernelFunction { get; }
}

public sealed class NativeChatFunction : ChatFunction
{
    public override ChatFunctionPermissions Permissions { get; }

    public override KernelFunction KernelFunction { get; }

    public NativeChatFunction(Delegate method, ChatFunctionPermissions permissions, LucideIconKind? icon = null)
    {
        Icon = icon;
        Permissions = permissions;
        KernelFunction = KernelFunctionFactory.CreateFromMethod(method);
    }
}