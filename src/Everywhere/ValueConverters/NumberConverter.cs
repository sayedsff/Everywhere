using Avalonia.Data.Converters;

namespace Everywhere.ValueConverters;

public static class NumberConverters
{
    public static IValueConverter PlusOne { get; } = new BidirectionalFuncValueConverter<int, int>(
        convert: x => x + 1,
        convertBack: x => x - 1
    );

    public static IValueConverter NotGreaterThanOne { get; } = new FuncValueConverter<int, bool>(
        convert: x => x <= 1
    );

    public static IValueConverter GreaterThanOne { get; } = new FuncValueConverter<int, bool>(
        convert: x => x > 1
    );
}