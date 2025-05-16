using Lucide.Avalonia;

namespace Everywhere.Models;

public record AssistantAttachment(
    IVisualElement Element,
    LucideIconKind Icon,
    DynamicResourceKey HeaderKey);