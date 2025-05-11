using System.Drawing;
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

    /// <summary>
    /// Relative to the screen pixels, regardless of the parent element.
    /// </summary>
    PixelRect BoundingRectangle { get; }

    int ProcessId { get; }

    /// <summary>
    /// get text content of the visual element.
    /// </summary>
    /// <param name="maxLength">allowed max length of the text, -1 means no limit.</param>
    /// <returns></returns>
    /// <remarks>
    /// set maxLength to 0 and check if the text is empty to check if the element is empty, with minimal performance impact.
    /// </remarks>
    string? GetText(int maxLength = -1);

    void SetText(string text, bool append);

    Task<Bitmap> CaptureAsync();

    event Action<IVisualElement> TextChanged;

    event Action<IVisualElement> BoundingRectangleChanged;
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