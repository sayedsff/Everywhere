using System.Numerics;
using Avalonia.Data.Converters;
using ZLinq;

namespace Everywhere.ValueConverters;

public class NumberConverters<T> where T : struct, INumber<T>
{
    private static T ChangeType(object? value) => (T)(Convert.ChangeType(value, typeof(T)) ?? default(T));

    public static IValueConverter Plus { get; } = new BidirectionalFuncValueConverter<T, T>(
        convert: static (x, p) => x + ChangeType(p),
        convertBack: static (x, p) => x - ChangeType(p)
    );

    public static IValueConverter Multiply { get; } = new BidirectionalFuncValueConverter<T, T>(
        convert: static (x, p) => x * ChangeType(p),
        convertBack: static (x, p) => x / ChangeType(p)
    );

    public static IValueConverter NotGreaterThan { get; } = new BidirectionalFuncValueConverter<T, bool>(
        convert: static (x, p) => x <= ChangeType(p),
        convertBack: static (_, _) => throw new NotSupportedException()
    );

    public static IValueConverter GreaterThan { get; } = new BidirectionalFuncValueConverter<T, bool>(
        convert: static (x, p) => x > ChangeType(p),
        convertBack: static (_, _) => throw new NotSupportedException()
    );

    /// <summary>
    /// Multi-value converter that returns true if the first value is smaller than any subsequent values.
    /// </summary>
    public static IMultiValueConverter MultiSmallerThanAny { get; } = new FuncMultiValueConverter<T, bool>(
        // ReSharper disable PossibleMultipleEnumeration
        numbers => numbers.AsValueEnumerable().First() < numbers.AsValueEnumerable().Skip(1).Min()
        // ReSharper restore PossibleMultipleEnumeration
    );

    public static IValueConverter FromEnum { get; } = new FromEnumConverter();

    private sealed class FromEnumConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            return value is null ? default : ChangeType(value);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            return value is null ? 0 : Enum.ToObject(targetType, System.Convert.ChangeType(value, TypeCode.Int64));
        }
    }
}

public class Int32Converters : NumberConverters<int>;
public class Int64Converters : NumberConverters<long>;
public class DoubleConverters : NumberConverters<double>;
public class SingleConverters : NumberConverters<float>;
public class DecimalConverters : NumberConverters<decimal>;