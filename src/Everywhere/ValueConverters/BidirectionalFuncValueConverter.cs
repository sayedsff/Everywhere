using System.Globalization;
using Avalonia.Data.Converters;

namespace Everywhere.ValueConverters;

public class BidirectionalFuncValueConverter<TInput, TOutput>(Func<TInput, object?, TOutput> convert, Func<TOutput, object?, TInput> convertBack)
    : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TInput input)
        {
            return convert(input, parameter);
        }

        if (System.Convert.ChangeType(value, typeof(TInput)) is TInput converted)
        {
            return convert(converted, parameter);
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TOutput output)
        {
            return convertBack(output, parameter);
        }

        if (System.Convert.ChangeType(value, typeof(TOutput)) is TOutput converted)
        {
            return convertBack(converted, parameter);
        }

        return null;
    }
}