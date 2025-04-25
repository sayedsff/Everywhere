using Everywhere.Enums;

namespace Everywhere.Interfaces;

public interface IVisualElement
{
    /// <summary>
    /// Unique identifier in one Visual Tree.
    /// </summary>
    string Id { get; }

    IVisualElement? Parent { get; }

    IEnumerable<IVisualElement> Children { get; }

    VisualElementType Type { get; }

    VisualElementStates States { get; }

    string? Name { get; }

    string? Text { get; set; }

    /// <summary>
    /// Relative to the screen pixels, regardless of the parent element.
    /// </summary>
    PixelRect BoundingRectangle { get; }

    uint ProcessId { get; }
}

public static class VisualElementExtension
{
    public static IEnumerable<IVisualElement> GetDescendants(this IVisualElement element)
    {
        foreach (var child in element.Children)
        {
            yield return child;
            foreach (var descendant in child.GetDescendants())
            {
                yield return descendant;
            }
        }
    }

    public static IEnumerable<IVisualElement> GetAncestors(this IVisualElement element)
    {
        var current = element.Parent;
        while (current != null)
        {
            yield return current;
            current = current.Parent;
        }
    }
}