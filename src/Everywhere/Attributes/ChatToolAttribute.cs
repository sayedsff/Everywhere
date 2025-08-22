using Everywhere.Enums;

namespace Everywhere.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class NativeChatFunctionAttribute : Attribute
{
    public string? Name { get; set; }

    public string? Description { get; set; }

    public object? Icon { get; set; }

    public required ChatFunctionPermissions Permissions { get; set; }
}