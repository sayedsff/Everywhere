using Avalonia.Markup.Xaml;

namespace Everywhere.MarkupExtensions;

public class ServiceLocatorExtension : MarkupExtension
{
    public required Type Type { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return ServiceLocator.Resolve(Type);
    }
}