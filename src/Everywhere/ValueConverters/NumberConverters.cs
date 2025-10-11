using System.Numerics;
using Avalonia.Data.Converters;

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
}

public class Int32Converters : NumberConverters<int>;
public class Int64Converters : NumberConverters<long>;
public class DoubleConverters : NumberConverters<double>;
public class SingleConverters : NumberConverters<float>;
public class DecimalConverters : NumberConverters<decimal>;