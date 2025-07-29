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
    public object?[] Arguments { get; set; } = [];

    public I18NExtension() { }

    [SetsRequiredMembers]
    public I18NExtension(object key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return new Binding
        {
            Path = "Self^",
            Source = Arguments is { Length: > 0 } args ? new FormattedDynamicResourceKey(Key, args) : new DynamicResourceKey(Key)
        };
    }
}