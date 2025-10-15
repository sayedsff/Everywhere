using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Everywhere.Common;
using Lucide.Avalonia;
using ZLinq;

namespace Everywhere.Views;

[TemplatePart(Name = "PART_IconTypeTabControl", Type = typeof(TabControl))]
public class IconEditor : TemplatedControl
{
    public static IReadOnlyList<LucideIconKind> AvailableKinds => Enum.GetValues<LucideIconKind>()
        .AsValueEnumerable()
        .Where((_, i) => i % 4 == 0)
        .Take(400)
        .ToList();

    public static IReadOnlyList<string> AvailableEmojis =>
    [
        "😀", "😂", "😍", "🤔", "😎", "😭", "👍", "🙏", "🎉", "🔥",
        "💡", "🚀", "🌟", "⚡", "💻", "📱", "🎨", "🎵", "📚", "🍔",
    ];

    public static readonly StyledProperty<ColoredIcon?> IconProperty = AvaloniaProperty.Register<IconEditor, ColoredIcon?>(nameof(Icon));

    public ColoredIcon? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    private IDisposable? _iconTypeTabControlSelectionChangedSubscription;
    private TabControl? _iconTypeTabControl;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _iconTypeTabControlSelectionChangedSubscription?.Dispose();
        _iconTypeTabControl = e.NameScope.Find<TabControl>("PART_IconTypeTabControl").NotNull();
        _iconTypeTabControlSelectionChangedSubscription = _iconTypeTabControl.AddDisposableHandler(
            SelectingItemsControl.SelectionChangedEvent,
            (_, args) =>
            {
                if (args.AddedItems is [TabItem { Tag: ColoredIconType type }]) Icon?.Type = type;
            },
            handledEventsToo: true);
        SetIconTypeTabControlSelection(Icon?.Type ?? ColoredIconType.Lucide);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IconProperty)
        {
            SetIconTypeTabControlSelection(Icon?.Type ?? ColoredIconType.Lucide);
        }
    }

    private void SetIconTypeTabControlSelection(ColoredIconType type)
    {
        if (_iconTypeTabControl?.Items is not IEnumerable items) return;
        var tabItem = items.OfType<TabItem>().FirstOrDefault(ti => ti.Tag is ColoredIconType t && t == type);
        if (tabItem != null) _iconTypeTabControl.SelectedItem = tabItem;
    }
}