using Avalonia.Media.Imaging;

namespace Everywhere.Interfaces;

public enum VisualElementType
{
    Unknown,
    Label,
    TextEdit,
    Document,
    Button,
    Hyperlink,
    Image,
    CheckBox,
    RadioButton,
    ComboBox,
    ListView,
    ListViewItem,
    TreeView,
    TreeViewItem,
    DataGrid,
    DataGridItem,
    TabControl,
    TabItem,
    Table,
    TableRow,
    Menu,
    MenuItem,
    Slider,
    ScrollBar,
    ProgressBar,
    Panel,
    TopLevel,
    Screen
}

[Flags]
public enum VisualElementStates
{
    None = 0,
    Offscreen = 1 << 0,
    Disabled = 1 << 1,
    Focused = 1 << 2,
    Selected = 1 << 3,
    ReadOnly = 1 << 4,
    Password = 1 << 5,
}

public interface IVisualElement
{
    IVisualElementContext Context { get; }

    /// <summary>
    /// Unique identifier in one Visual Tree.
    /// </summary>
    string Id { get; }

    IVisualElement? Parent { get; }

    IEnumerable<IVisualElement> Children { get; }

    IVisualElement? PreviousSibling { get; }

    IVisualElement? NextSibling { get; }

    VisualElementType Type { get; }

    VisualElementStates States { get; }

    string? Name { get; }

    /// <summary>
    /// Relative to the screen pixels, regardless of the parent element.
    /// </summary>
    PixelRect BoundingRectangle { get; }

    int ProcessId { get; }

    nint NativeWindowHandle { get; }

    /// <summary>
    /// get text content of the visual element.
    /// </summary>
    /// <param name="maxLength">allowed max length of the text, -1 means no limit.</param>
    /// <returns></returns>
    /// <remarks>
    /// set maxLength to 1 can check if the text is null or empty, with minimal performance impact.
    /// </remarks>
    string? GetText(int maxLength = -1);

    void SetText(string text, bool append);

    Task<Bitmap> CaptureAsync();
}

public static class VisualElementExtension
{
    public static IEnumerable<IVisualElement> GetDescendants(this IVisualElement element, bool includeSelf = false)
    {
        if (includeSelf)
        {
            yield return element;
        }

        foreach (var child in element.Children)
        {
            yield return child;
            foreach (var descendant in child.GetDescendants())
            {
                yield return descendant;
            }
        }
    }

    public static IEnumerable<IVisualElement> GetAncestors(this IVisualElement element, bool includeSelf = false)
    {
        var current = includeSelf ? element : element.Parent;
        while (current != null)
        {
            yield return current;
            current = current.Parent;
        }
    }
}