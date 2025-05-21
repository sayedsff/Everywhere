using System.ComponentModel;
using System.Text;
using Avalonia.Media.Imaging;

namespace Everywhere.Models;

public class OptimizedVisualElement : IVisualElement
{
    public static OptimizedVisualElement Create(IVisualElement element) =>
        element as OptimizedVisualElement ?? new OptimizedVisualElement(element);

    public string Id => original.Id;

    public IVisualElement? Parent => GetOptimizedParent(original.Parent);

    public IEnumerable<IVisualElement> Children => GetOptimizedChildren(original);

    public VisualElementType Type => original.Type;

    public VisualElementStates States => original.States;

    public string? Name => original.Name;

    public PixelRect BoundingRectangle => original.BoundingRectangle;

    public int ProcessId => original.ProcessId;

    private readonly IVisualElement original;

    private OptimizedVisualElement(IVisualElement original)
    {
        this.original = original;
    }

    public string? GetText(int maxLength = -1) => original.GetText(maxLength);

    public void SetText(string text, bool append) => original.SetText(text, append);

    public Task<Bitmap> CaptureAsync() => original.CaptureAsync();

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add => original.PropertyChanged += value;
        remove => original.PropertyChanged -= value;
    }

    private static OptimizedVisualElement? GetOptimizedParent(IVisualElement? parent)
    {
        while (parent != null)
        {
            if (IsElementVisible(parent) &&
                IsElementImportant(parent) ||
                IsPanelLikeElement(parent) && !string.IsNullOrEmpty(parent.Name))
            {
                break;
            }

            parent = parent.Parent;
        }

        return parent != null ? new OptimizedVisualElement(parent) : null;
    }

    private static IEnumerable<IVisualElement> GetOptimizedChildren(IVisualElement element)
    {
        List<IVisualElement>? labels = null;
        using var enumerator = element.Children.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var child = enumerator.Current;

            // compose multiple label into one
            if (child.Type == VisualElementType.Label)
            {
                // remove the label if it is not visible or no text
                if (!IsElementVisible(child) ||
                    string.IsNullOrEmpty(element.GetText(1)))
                {
                    continue;
                }

                var first = child;

                while (enumerator.MoveNext())
                {
                    child = enumerator.Current;

                    if (child.Type != VisualElementType.Label) break;
                    if (!IsElementVisible(child)) continue; // skip invisible label

                    labels ??= [];
                    if (labels.Count == 0) labels.Add(first);
                    labels.Add(child);
                }

                if (labels is { Count: > 0 })
                {
                    yield return new OptimizedLabelVisualElement(labels);
                    labels.Clear();
                }
                else
                {
                    yield return first;
                }
            }

            if (!IsElementVisible(child)) continue;

            if (IsElementImportant(child))
            {
                yield return Create(child);
            }
            else if (IsPanelLikeElement(child) && string.IsNullOrEmpty(child.Name))
            {
                // Flatten the panel if it has no name and no text.
                // Skip the panel and add its children directly.
                foreach (var optimizedChild in GetOptimizedChildren(child))
                {
                    yield return optimizedChild;
                }
            }
        }
    }

    private static bool IsElementVisible(IVisualElement element)
    {
        var boundingRectangle = element.BoundingRectangle;

        if (boundingRectangle.Width <= 0 || boundingRectangle.Height <= 0)
            return false;

        if (boundingRectangle.X + boundingRectangle.Width <= 0 ||
            boundingRectangle.Y + boundingRectangle.Height <= 0)
            return false;

        if (element.States.HasFlag(VisualElementStates.Offscreen))
            return false;

        return true;
    }

    private static bool IsElementImportant(IVisualElement element)
    {
        // Interactive elements are always included
        if (IsElementTypeImportant(element.Type))
            return true;

        // Check if the element has a name or text
        if (!string.IsNullOrEmpty(element.Name) ||
            !string.IsNullOrEmpty(element.GetText(1)))
            return true;

        return false;
    }

    private static bool IsElementTypeImportant(VisualElementType type)
    {
        return type switch
        {
            VisualElementType.Button => true,
            VisualElementType.TextEdit => true,
            VisualElementType.Document => true,
            VisualElementType.CheckBox => true,
            VisualElementType.RadioButton => true,
            VisualElementType.ComboBox => true,
            VisualElementType.ListView => true,
            VisualElementType.TreeView => true,
            VisualElementType.DataGrid => true,
            VisualElementType.TabControl => true,
            VisualElementType.TabItem => true,
            VisualElementType.Menu => true,
            VisualElementType.MenuItem => true,
            VisualElementType.Slider => true,
            VisualElementType.Table => true,
            VisualElementType.TableRow => true,
            VisualElementType.Image => true,
            _ => false
        };
    }

    private static bool IsPanelLikeElement(IVisualElement element)
    {
        return element.Type switch
        {
            VisualElementType.Panel => true,
            VisualElementType.TopLevel => true,
            VisualElementType.Unknown when element.Children.Any() => true,
            _ => false
        };
    }

    public override int GetHashCode() => original.GetHashCode();
    public override bool Equals(object? obj) => ReferenceEquals(this, obj) || obj is OptimizedVisualElement other && original.Equals(other.original);
    public override string? ToString() => original.ToString();

    private class OptimizedLabelVisualElement(List<IVisualElement> items) : IVisualElement
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public string Id => items[0].Id;
        public IVisualElement? Parent => GetOptimizedParent(items[0].Parent);
        public IEnumerable<IVisualElement> Children => [];
        public VisualElementType Type => VisualElementType.Label;
        public VisualElementStates States => VisualElementStates.None;
        public string? Name => null;
        public PixelRect BoundingRectangle => items.Select(i => i.BoundingRectangle).Aggregate(new PixelRect(), (a, b) => a.Union(b));
        public int ProcessId => items[0].ProcessId;

        public string GetText(int maxLength = -1)
        {
            if (maxLength == 0) return string.Empty;
            var lengthLeft = maxLength;
            var sb = new StringBuilder();
            foreach (var text in items.Select(i => i.GetText(lengthLeft)))
            {
                sb.Append(text ?? " ");
                if (lengthLeft <= 0 || text == null) continue;
                lengthLeft -= text.Length;
                if (lengthLeft <= 0) break;
            }
            return sb.ToString();
        }

        public void SetText(string text, bool append) { }

        public Task<Bitmap> CaptureAsync()
        {
            throw new NotSupportedException();
        }
    }
}