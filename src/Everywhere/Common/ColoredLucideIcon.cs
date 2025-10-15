using CommunityToolkit.Mvvm.ComponentModel;
using Lucide.Avalonia;

namespace Everywhere.Common;

public partial class ColoredLucideIcon(LucideIconKind kind, SerializableColor? foreground = null, SerializableColor? background = null)
    : ObservableObject
{
    [ObservableProperty]
    public partial LucideIconKind Kind { get; set; } = kind;

    [ObservableProperty]
    public partial SerializableColor? Foreground { get; set; } = foreground;

    [ObservableProperty]
    public partial SerializableColor? Background { get; set; } = background;
}