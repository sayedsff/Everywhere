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
        convert: (x, _) => Uri.TryCreate(x, UriKind.RelativeOrAbsolute, out var uri) ? uri : null,
        convertBack: (x, _) => x?.ToString()
    );

    public static IValueConverter DateTimeOffsetToString { get; } = new BidirectionalFuncValueConverter<DateTimeOffset, string>(
            convert: (x, p) => x.DateTime.ToLocalTime().ToString(p?.ToString()),
            convertBack: (x, p) => DateTimeOffset.ParseExact(x, p?.ToString() ?? "o", null)
        );

    public static IValueConverter FullPathToFileName { get; } = new FuncValueConverter<string, string?>(
        convert: x => Path.GetFileName(x) is { Length: > 0 } fileName ? fileName : x // return original if no file name found (e.g. Path root)
    );

    public static IMultiValueConverter DefaultMultiValue { get; } = new DefaultMultiValueConverter();

    public static IMultiValueConverter AllEquals { get; } = new AllEqualsConverter();

    /// <summary>
    /// Returns the first non-null and non-UnsetValue value from the input values.
    /// </summary>
    public static IMultiValueConverter FirstNotNull { get; } = new FirstNonNullConverter();

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