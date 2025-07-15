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
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TOutput output)
        {
            return convertBack(output);
        }
        return null;
    }
}