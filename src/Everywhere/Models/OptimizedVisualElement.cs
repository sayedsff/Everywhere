using System.ComponentModel;
using Avalonia.Media.Imaging;

namespace Everywhere.Models;

public class OptimizedVisualElement(IVisualElement original) : IVisualElement
{
    public string Id => original.Id;

    public IVisualElement? Parent
    {
        get
        {
            var parent = original.Parent;
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
    }

    public IEnumerable<IVisualElement> Children => GetOptimizedChildren(original);

    public VisualElementType Type => original.Type;

    public VisualElementStates States => original.States;

    public string? Name => original.Name;

    public PixelRect BoundingRectangle => original.BoundingRectangle;

    public int ProcessId => original.ProcessId;

    public string? GetText(int maxLength = -1) => original.GetText(maxLength);

    public void SetText(string text, bool append) => original.SetText(text, append);

    public Task<Bitmap> CaptureAsync() => original.CaptureAsync();

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add => original.PropertyChanged += value;
        remove => original.PropertyChanged -= value;
    }

    private static IEnumerable<IVisualElement> GetOptimizedChildren(IVisualElement element)
    {
        foreach (var child in element.Children)
        {
            if (!IsElementVisible(child)) continue;

            if (IsElementImportant(child))
            {
                yield return new OptimizedVisualElement(child);
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
    public override bool Equals(object? obj) => original.Equals(obj);
    public override string? ToString() => original.ToString();
}