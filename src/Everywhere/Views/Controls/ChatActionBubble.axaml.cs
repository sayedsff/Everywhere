using Avalonia.Controls;
using Lucide.Avalonia;

namespace Everywhere.Views;

public class ChatActionBubble : ContentControl
{
    public static readonly StyledProperty<bool> IsBusyProperty =
        AvaloniaProperty.Register<ChatActionBubble, bool>(nameof(IsBusy));

    public bool IsBusy
    {
        get => GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    public static readonly StyledProperty<bool> IsErrorProperty =
        AvaloniaProperty.Register<ChatActionBubble, bool>(nameof(IsError));

    public bool IsError
    {
        get => GetValue(IsErrorProperty);
        set => SetValue(IsErrorProperty, value);
    }

    public static readonly StyledProperty<object?> ErrorContentProperty =
        AvaloniaProperty.Register<ChatActionBubble, object?>(nameof(ErrorContent));

    public object? ErrorContent
    {
        get => GetValue(ErrorContentProperty);
        set => SetValue(ErrorContentProperty, value);
    }

    public static readonly StyledProperty<double> ElapsedSecondsProperty =
        AvaloniaProperty.Register<ChatActionBubble, double>(nameof(ElapsedSeconds));

    public double ElapsedSeconds
    {
        get => GetValue(ElapsedSecondsProperty);
        set => SetValue(ElapsedSecondsProperty, value);
    }

    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<ChatActionBubble, object?>(nameof(Header));

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly StyledProperty<LucideIconKind> IconProperty =
        AvaloniaProperty.Register<ChatActionBubble, LucideIconKind>(nameof(Icon));

    public LucideIconKind Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
}