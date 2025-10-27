using System.Globalization;
using System.Reflection;
using Avalonia.Data.Converters;

namespace Everywhere.ValueConverters;

/// <summary>
/// Resolves a <see cref="DynamicResourceKeyAttribute"/> to the actual resource.
/// </summary>
public class DynamicResourceKeyConverter : IValueConverter
{
    public static DynamicResourceKeyConverter Shared { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // 1. value is an object with DynamicResourceKeyAttribute on class
        // 2. value is an enum with DynamicResourceKeyAttribute on field

        if (value is null) return null;

        var type = value.GetType();
        DynamicResourceKeyAttribute? attribute;
        if (type.IsEnum)
        {
            attribute = type.GetField(value.ToString() ?? string.Empty)?.GetCustomAttributes<DynamicResourceKeyAttribute>(true).FirstOrDefault();
        }
        else
        {
            attribute = type.GetCustomAttributes<DynamicResourceKeyAttribute>(true).FirstOrDefault();
        }

        return attribute is null ? null : new DynamicResourceKey(attribute.HeaderKey);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}