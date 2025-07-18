using System.Globalization;
using Avalonia.Data.Converters;

namespace Everywhere.ValueConverters;

public class BidirectionalFuncValueConverter<TInput, TOutput>(Func<TInput, TOutput> convert, Func<TOutput, TInput> convertBack) : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TInput input)
        {
            return convert(input);
        }

        if (System.Convert.ChangeType(value, typeof(TInput)) is TInput converted)
        {
            return convert(converted);
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TOutput output)
        {
            return convertBack(output);
        }

        if (System.Convert.ChangeType(value, targetType) is TOutput converted)
        {
            return convertBack(converted);
        }

        return null;
    }
}