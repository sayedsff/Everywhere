using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Reactive;
using Everywhere.Models;

namespace Everywhere.Views;

public class DynamicKeyMenuItem : MenuItem
{
    public static IDataTemplate DefaultItemTemplate { get; } = new FuncDataTemplate<object>(
        (item, _) => item switch
        {
            DynamicKeyMenuItem assistantAttachmentItem => assistantAttachmentItem,
            MenuItem menuItem => new DynamicKeyMenuItem
            {
                [!HeaderProperty] = menuItem[!HeaderProperty],
                [!IconProperty] = menuItem[!IconProperty],
                [!CommandProperty] = menuItem[!CommandProperty],
                [!CommandParameterProperty] = menuItem[!CommandParameterProperty],
                [!IsEnabledProperty] = menuItem[!IsEnabledProperty],
                [!IsCheckedProperty] = menuItem[!IsCheckedProperty]
            },
            _ => new DynamicKeyMenuItem
            {
                Header = item
            }
        });

    public static readonly DirectProperty<DynamicKeyMenuItem, DynamicResourceKey?> HeaderKeyProperty =
        AvaloniaProperty.RegisterDirect<DynamicKeyMenuItem, DynamicResourceKey?>(
            nameof(HeaderKey), o => o.HeaderKey);

    public DynamicResourceKey HeaderKey => Header as DynamicResourceKey ?? new DynamicResourceKey(Header?.ToString() ?? string.Empty);

    static DynamicKeyMenuItem()
    {
        ItemTemplateProperty.OverrideDefaultValue<DynamicKeyMenuItem>(DefaultItemTemplate);

        HeaderProperty.Changed.Subscribe(new AnonymousObserver<AvaloniaPropertyChangedEventArgs<object?>>(e =>
        {
            if (e.Sender is not DynamicKeyMenuItem dynamicKeyMenuItem) return;
            dynamicKeyMenuItem.RaisePropertyChanged(HeaderKeyProperty, null, dynamicKeyMenuItem.HeaderKey);
        }));
    }
}