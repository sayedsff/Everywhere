using Everywhere.Enums;

namespace Everywhere.Models;

public class OptimizedVisualElement(IVisualElement original) : IVisualElement
{
    public string Id => original.Id;

    public IVisualElement? Parent { get; private init; }

    public IEnumerable<IVisualElement> Children => GetOptimizedChildren(original);

    public VisualElementType Type => original.Type;

    public VisualElementStates States => original.States;

    public string? Name => original.Name;

    public PixelRect BoundingRectangle => original.BoundingRectangle;

    public int ProcessId => original.ProcessId;

    public string? GetText(int maxLength = -1) => original.GetText(maxLength);

    public void SetText(string text, bool append) => original.SetText(text, append);

    public event Action<IVisualElement>? TextChanged
    {
        add => original.TextChanged += value;
        remove => original.TextChanged -= value;
    }

    public event Action<IVisualElement>? BoundingRectangleChanged
    {
        add => original.BoundingRectangleChanged += value;
        remove => original.BoundingRectangleChanged -= value;
    }

    private static IEnumerable<IVisualElement> GetOptimizedChildren(IVisualElement element)
    {
        foreach (var child in element.Children)
        {
            if (element.BoundingRectangle is not { Width: > 0, Height: > 0 })
                continue;

            if (IsPanelLikeElement(child) &&
                string.IsNullOrEmpty(child.Name))
            {
                // Flatten the panel if it has no name and no text
                // Skip the panel and add its children directly
                foreach (var optimizedChild in GetOptimizedChildren(child))
                {
                    yield return optimizedChild;
                }
            }
            else if (ShouldIncludeElement(child))
            {
                yield return new OptimizedVisualElement(child)
                {
                    Parent = element
                };
            }
        }
    }

    private static bool ShouldIncludeElement(IVisualElement element)
    {
        // Interactive elements are always included
        if (IsInteractiveElement(element.Type) ||
            IsSignificantElementType(element.Type))
            return true;

        // Check if the element has a name or text
        if (!string.IsNullOrEmpty(element.Name) ||
            element.GetText(0) != null)
            return true;

        return false;
    }

    private static bool IsInteractiveElement(VisualElementType type)
    {
        return type switch
        {
            VisualElementType.Button => true,
            VisualElementType.TextEdit => true,
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
            VisualElementType.ScrollBar => true,
            _ => false
        };
    }

    private static bool IsSignificantElementType(VisualElementType type)
    {
        return type switch
        {
            VisualElementType.TextEdit => true,
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