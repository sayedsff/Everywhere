using Avalonia.Controls.Documents;
using SukiUI.Controls;

namespace Everywhere.Collections;

public class BusyInlineCollection : InlineCollection
{
    public bool IsBusy
    {
        get => loading.IsVisible;
        set => loading.IsVisible = value;
    }

    private readonly Loading loading;

    public BusyInlineCollection()
    {
        Add(loading = new Loading
        {
            Width = 16,
            Height = 16,
            IsHitTestVisible = false
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