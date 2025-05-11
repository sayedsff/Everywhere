using Avalonia.Controls;
using Avalonia.Reactive;
using Everywhere.Models;

namespace Everywhere.Views;

public class DynamicKeyMenuItem : MenuItem
{
    public static readonly DirectProperty<DynamicKeyMenuItem, DynamicResourceKey?> HeaderKeyProperty =
        AvaloniaProperty.RegisterDirect<DynamicKeyMenuItem, DynamicResourceKey?>(
            nameof(HeaderKey), o => o.HeaderKey);

    public DynamicResourceKey HeaderKey => new(Header?.ToString() ?? string.Empty);

    public DynamicKeyMenuItem()
    {
        HeaderProperty.Changed.Subscribe(new AnonymousObserver<AvaloniaPropertyChangedEventArgs<object?>>(
            _ => RaisePropertyChanged(HeaderKeyProperty, null, HeaderKey)));
    }
}