using System.Globalization;
using Avalonia.Data.Converters;

namespace Everywhere.ValueConverters;

public class ParameterizedFuncValueConverter<TInput, TOutput>(Func<TInput, object?, TOutput> convert) : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TInput input)
        {
            return convert(input, parameter);
        }

        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}