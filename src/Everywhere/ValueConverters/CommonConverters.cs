using Avalonia.Data.Converters;

namespace Everywhere.ValueConverters;

public static class CommonConverters
{
    public static IValueConverter TypeEquals { get; } = new ParameterizedFuncValueConverter<object?, bool>(
        convert: (x, parameter) => x?.GetType() == parameter as Type
    );

    public static IValueConverter StringToUri { get; } = new BidirectionalFuncValueConverter<string?, Uri?>(
        convert: x => x == null ? null : new Uri(x, UriKind.RelativeOrAbsolute),
        convertBack: x => x?.ToString()
    );

    public static IValueConverter DateTimeOffsetToString { get; } = new DateTimeOffsetToStringConverter();

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
}