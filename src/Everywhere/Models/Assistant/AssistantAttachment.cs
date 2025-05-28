using Avalonia.Media.Imaging;
using Lucide.Avalonia;

namespace Everywhere.Models;

public abstract record AssistantAttachment(LucideIconKind Icon, DynamicResourceKey HeaderKey);

public record AssistantVisualElementAttachment(IVisualElement Element, LucideIconKind Icon, DynamicResourceKey HeaderKey) :
    AssistantAttachment(Icon, HeaderKey);

public record AssistantTextAttachment(string Text, DynamicResourceKey HeaderKey) : AssistantAttachment(LucideIconKind.Text, HeaderKey);

public record AssistantImageAttachment(Bitmap Image, DynamicResourceKey HeaderKey) : AssistantAttachment(LucideIconKind.Image, HeaderKey);

public record AssistantFileAttachment(string FilePath, DynamicResourceKey HeaderKey) : AssistantAttachment(LucideIconKind.File, HeaderKey);