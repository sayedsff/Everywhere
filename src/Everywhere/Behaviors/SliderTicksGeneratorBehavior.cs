using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Xaml.Interactivity;

namespace Everywhere.Behaviors;

/// <summary>
/// A behavior that makes a Slider control operate on a logarithmic scale.
/// </summary>
public class LogarithmicSliderBehavior : Behavior<Slider>
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<LogarithmicSliderBehavior, double>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<LogarithmicSliderBehavior, double>(nameof(Minimum), 1);

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<LogarithmicSliderBehavior, double>(nameof(Maximum), 100);

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    private bool _isUpdating;

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject != null)
        {
            AssociatedObject.Minimum = 0;
            AssociatedObject.Maximum = 100;
            AssociatedObject.PropertyChanged += OnSliderPropertyChanged;
            UpdateSliderValue();
        }
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        if (AssociatedObject != null)
        {
            AssociatedObject.PropertyChanged -= OnSliderPropertyChanged;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty ||
            change.Property == MinimumProperty ||
            change.Property == MaximumProperty)
        {
            UpdateSliderValue();
        }
    }

    private void OnSliderPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == RangeBase.ValueProperty)
        {
            UpdateActualValue();
        }
    }

    private void UpdateSliderValue()
    {
        if (_isUpdating || AssociatedObject == null) return;

        var min = Minimum;
        var max = Maximum;
        var val = Value;

        if (min <= 0 || max <= min) return;

        var logMin = Math.Log(min);
        var logMax = Math.Log(max);
        var logVal = Math.Log(Math.Clamp(val, min, max));

        var sliderValue = 100 * (logVal - logMin) / (logMax - logMin);

        _isUpdating = true;
        AssociatedObject.Value = sliderValue;
        _isUpdating = false;
    }

    private void UpdateActualValue()
    {
        if (_isUpdating || AssociatedObject == null) return;

        var min = Minimum;
        var max = Maximum;

        if (min <= 0 || max <= min) return;

        var logMin = Math.Log(min);
        var logMax = Math.Log(max);
        var sliderValue = AssociatedObject.Value;

        var logVal = logMin + (logMax - logMin) * sliderValue / 100;
        var newActualValue = Math.Round(Math.Exp(logVal));

        _isUpdating = true;
        Value = newActualValue;
        _isUpdating = false;
    }
}
