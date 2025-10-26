using System.Reflection;

namespace Everywhere.Extensions;

public static class I18NExtension
{
    public static string I18N(this string key) => DynamicResourceKey.Resolve(key);

    public static string I18N(this string key, params DynamicResourceKeyBase[] args) => new FormattedDynamicResourceKey(key, args).ToString();

    /// <summary>
    /// Resolves the enum value to its internationalized string representation.
    /// </summary>
    /// <param name="e"></param>
    /// <param name="separator">If the enum is a [Flags], the separator to use between multiple values.</param>
    /// <param name="preferMinimalSet"></param>
    /// <returns></returns>
    public static string I18N(this Enum e, string separator = ", ", bool preferMinimalSet = false)
    {
        var type = e.GetType();
        var attribute = type.GetField(e.ToString())?.GetCustomAttributes<DynamicResourceKeyAttribute>(true).FirstOrDefault();
        if (attribute is null) return e.ToString();

        var isFlags = type.GetCustomAttribute<FlagsAttribute>() is not null;
        if (!isFlags) return DynamicResourceKey.Resolve(attribute.Key);

        var values = Enum.GetValues(type).Cast<Enum>();
        if (preferMinimalSet)
        {
            // Get the minimal set of flags that make up the enum value
            // e.g.
            // Read | Write | Execute => ReadWriteExecute (instead of ReadWriteExecute, ReadWrite, Read, Write, Execute)
            // Read | Execute => Read, Execute (because there's no ReadExecute flag)

            var target = Convert.ToInt64(e);
            var results = new List<Enum>();
            foreach (var v in values)
            {
                var val = Convert.ToInt64(v);
                if ((target & val) == val)
                {
                    results.Add(v);
                    target -= val;
                }
                if (target == 0) break;
            }
            values = results;
        }
        else
        {
            values = values.Where(e.HasFlag);
        }

        var parts = values.Select(v =>
        {
            var attr = type.GetField(v.ToString())?.GetCustomAttributes<DynamicResourceKeyAttribute>(true).FirstOrDefault();
            return attr is null ? v.ToString() : DynamicResourceKey.Resolve(attr.Key);
        });
        return string.Join(separator, parts);
    }
}