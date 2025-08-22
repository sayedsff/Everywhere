using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Enums;
using Lucide.Avalonia;
using Microsoft.SemanticKernel;

namespace Everywhere.Models;

public abstract partial class ChatFunction : ObservableObject
{
    public LucideIconKind? Icon { get; set; }

    [ObservableProperty]
    public partial ChatFunctionPermissions? Permissions { get; set; }

    [ObservableProperty]
    public partial Customizable<ChatFunctionPermissions> AllowedPermissions { get; set; } = ChatFunctionPermissions.None;

    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = true;

    public abstract KernelFunction KernelFunction { get; }
}

public class AnonymousChatFunction : ChatFunction
{
    public override KernelFunction KernelFunction { get; }

    public AnonymousChatFunction(Delegate method, ChatFunctionPermissions permissions, LucideIconKind? icon = null)
    {
        Icon = icon;
        Permissions = permissions;
        KernelFunction = KernelFunctionFactory.CreateFromMethod(method);
    }
}