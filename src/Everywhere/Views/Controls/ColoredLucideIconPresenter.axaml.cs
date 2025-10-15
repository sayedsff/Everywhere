using Avalonia.Controls.Primitives;
using Everywhere.Common;

namespace Everywhere.Views;

public class ColoredLucideIconPresenter : TemplatedControl
{
    public static readonly StyledProperty<ColoredLucideIcon?> IconProperty =
        AvaloniaProperty.Register<ColoredLucideIconPresenter, ColoredLucideIcon?>(nameof(Icon));

    public ColoredLucideIcon? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly StyledProperty<double> IconSizeProperty = AvaloniaProperty.Register<ColoredLucideIconPresenter, double>(
        nameof(IconSize));

    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }
}