using Avalonia.Controls.Primitives;
using Everywhere.Common;

namespace Everywhere.Views;

public class IconPresenter : TemplatedControl
{
    public static readonly StyledProperty<ColoredIcon?> IconProperty =
        AvaloniaProperty.Register<IconPresenter, ColoredIcon?>(nameof(Icon));

    public ColoredIcon? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly StyledProperty<double> IconSizeProperty = AvaloniaProperty.Register<IconPresenter, double>(nameof(IconSize));

    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }
}