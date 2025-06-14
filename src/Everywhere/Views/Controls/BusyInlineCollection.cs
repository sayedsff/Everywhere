using Avalonia.Controls.Documents;
using Avalonia.Data.Converters;
using ShadUI.Controls;

namespace Everywhere.Views;

public class BusyInlineCollection : InlineCollection
{
    public static IValueConverter IsNotEmpty { get; } = new FuncValueConverter<int, bool>(i => i > 1);

    public bool IsBusy
    {
        get => loading.IsVisible;
        set => loading.IsVisible = value;
    }

    private readonly Loading loading;

    public BusyInlineCollection(bool isBusy = false)
    {
        Add(
            loading = new Loading
            {
                Width = 16,
                Height = 16,
                Margin = new Thickness(4, 0, 0, 0),
                IsHitTestVisible = false,
                IsVisible = isBusy
            });
    }

    public override void Add(Inline inline)
    {
        base.Insert(Math.Max(Count - 1, 0), inline);
    }

    public override void Clear()
    {
        base.RemoveRange(0, Count - 1);
    }
}