using Everywhere.Enums;

namespace Everywhere.Models;

public class OptimizedVisualElement(IVisualElement original) : IVisualElement
{
    public IVisualElement? Parent { get; private init; }

    public IEnumerable<IVisualElement> Children
    {
        get
        {
            if (optimizedChildren == null)
            {
                optimizedChildren = [];
                OptimizeAndFlattenChildren(original);
            }

            return optimizedChildren;
        }
    }

    public VisualElementType Type => original.Type;

    public VisualElementStates States => original.States;

    public string? Name => original.Name;

    public string? Text
    {
        get => original.Text;
        set => original.Text = value;
    }

    public PixelRect BoundingRectangle => original.BoundingRectangle;

    public uint ProcessId => original.ProcessId;

    private List<IVisualElement>? optimizedChildren;

    private void OptimizeAndFlattenChildren(IVisualElement element)
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
                OptimizeAndFlattenChildren(child);
            }
            else if (ShouldIncludeElement(child))
            {
                optimizedChildren!.Add(new OptimizedVisualElement(child)
                {
                    Parent = element
                });
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
            !string.IsNullOrEmpty(element.Text))
            return true;

        return false;
    }

    private static bool IsInteractiveElement(VisualElementType type)
    {
        return type switch
        {
            VisualElementType.Button => true,
            VisualElementType.TextBox => true,
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
            VisualElementType.Document => true,
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
}