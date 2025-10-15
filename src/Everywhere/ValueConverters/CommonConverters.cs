using Avalonia.Data.Converters;
using Avalonia.Media;
using Everywhere.Common;
using ZLinq;

namespace Everywhere.ValueConverters;

public static class CommonConverters
{
    public static IValueConverter TypeEquals { get; } = new ParameterizedFuncValueConverter<object?, bool>(
        convert: (x, parameter) => x?.GetType() == parameter as Type
    );

    public static IValueConverter StringToUri { get; } = new BidirectionalFuncValueConverter<string?, Uri?>(
        convert: (x, _) => x == null ? null : new Uri(x, UriKind.RelativeOrAbsolute),
        convertBack: (x, _) => x?.ToString()
    );

    public static IValueConverter DateTimeOffsetToString { get; } = new DateTimeOffsetToStringConverter();

    public static IMultiValueConverter DefaultMultiValue { get; } = new DefaultMultiValueConverter();

    public static IMultiValueConverter AllEquals { get; } = new AllEqualsConverter();

    public static IMultiValueConverter FirstNonNull { get; } = new FirstNonNullConverter();

    private class DateTimeOffsetToStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is DateTimeOffset dateTimeOffset)
            {
                return dateTimeOffset.DateTime.ToLocalTime().ToString(parameter?.ToString());
            }

            return null;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    private class DefaultMultiValueConverter : IMultiValueConverter
    {
        private readonly DefaultValueConverter _defaultValueConverter = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            var value = values.AsValueEnumerable().FirstOrDefault(v => v != AvaloniaProperty.UnsetValue) ?? parameter;
            return value switch
            {
                null => null,
                Color color when typeof(SolidColorBrush).IsAssignableTo(targetType) => new SolidColorBrush(color),
                SerializableColor color when typeof(SolidColorBrush).IsAssignableTo(targetType) => new SolidColorBrush(color),
                _ => _defaultValueConverter.Convert(value, targetType, null, culture)
            };
        }
    }

    private class AllEqualsConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            var first = values.AsValueEnumerable().FirstOrDefault(v => v != AvaloniaProperty.UnsetValue);
            return first != null && values.AsValueEnumerable().Skip(1).All(v => v == first);
        }
    }

    private class FirstNonNullConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            return values.AsValueEnumerable().OfType<object>().FirstOrDefault(value => value != AvaloniaProperty.UnsetValue);
        }
    }
}