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
}