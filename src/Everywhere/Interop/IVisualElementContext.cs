namespace Everywhere.Interop;

public enum PickElementMode
{
    /// <summary>
    /// Pick a whole screen.
    /// </summary>
    Screen,

    /// <summary>
    /// Pick a window.
    /// </summary>
    Window,

    /// <summary>
    /// Pick a specific element.
    /// </summary>
    Element
}

public interface IVisualElementContext
{
    public delegate void KeyboardFocusedElementChangedHandler(IVisualElement? element);

    event KeyboardFocusedElementChangedHandler KeyboardFocusedElementChanged;

    IVisualElement? KeyboardFocusedElement { get; }

    IVisualElement? PointerOverElement { get; }

    /// <summary>
    /// Get the element at the specified point.
    /// </summary>
    /// <param name="point">Point in screen pixels.</param>
    /// <returns></returns>
    IVisualElement? ElementFromPoint(PixelPoint point);

    /// <summary>
    /// Let the user pick an element from the screen.
    /// </summary>
    /// <returns></returns>
    Task<IVisualElement?> PickElementAsync(PickElementMode mode);
}