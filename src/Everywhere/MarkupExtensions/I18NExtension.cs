using System.Diagnostics.CodeAnalysis;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Metadata;
using Microsoft.Extensions.DependencyInjection;

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
        var target = serviceProvider.GetService<IProvideValueTarget>();
        var dynamicResourceKey = Arguments is { Length: > 0 } args ?
            new FormattedDynamicResourceKey(Key, args.Select(a => a switch
            {
                DynamicResourceKeyBase drk => drk,
                _ => new DynamicResourceKey(a)
            }).ToArray()) :
            new DynamicResourceKey(Key);
        return target?.TargetProperty is AvaloniaProperty ?
            dynamicResourceKey.ToBinding() :
            dynamicResourceKey.ToString() ?? string.Empty; // Only AvaloniaProperty can resolve binding, otherwise return resolved string
    }
}