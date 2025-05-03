using System.Globalization;
using Avalonia.Data.Converters;

namespace Everywhere.ValueConverters;

public static class NumberConverters
{
    public static IValueConverter IsNotZero { get; } = new IsNotZeroConverter();

    private class IsNotZeroConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value switch
            {
                null => false,
                int i => i != 0,
                double d => d != 0,
                float f => f != 0,
                decimal m => m != 0,
                _ => false
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}