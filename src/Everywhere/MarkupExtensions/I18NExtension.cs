using System.Diagnostics.CodeAnalysis;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Metadata;
using Everywhere.Models;

namespace Everywhere.MarkupExtensions;

public class I18NExtension : MarkupExtension
{
    public required object Key { get; set; }

    [Content, AssignBinding]
    public object[] Arguments { get; set; } = [];

    public I18NExtension() { }

    [SetsRequiredMembers]
    public I18NExtension(object key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var dynamicResourceKey = Arguments is { Length: > 0 } args ?
            new FormattedDynamicResourceKey(Key, args.Select(a => a switch
            {
                DynamicResourceKeyBase drk => drk,
                _ => new DynamicResourceKey(a)
            }).ToArray()) :
            new DynamicResourceKey(Key);
        return dynamicResourceKey.ToBinding();
    }
}