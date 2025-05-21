using Lucide.Avalonia;

namespace Everywhere.Models;

public record AssistantAttachment(LucideIconKind Icon, DynamicResourceKey HeaderKey);

public record AssistantVisualElementAttachment(IVisualElement Element, LucideIconKind Icon, DynamicResourceKey HeaderKey) :
    AssistantAttachment(Icon, HeaderKey);