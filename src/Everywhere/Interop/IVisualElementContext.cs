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
    delegate void KeyboardFocusedElementChangedHandler(IVisualElement? element);

    event KeyboardFocusedElementChangedHandler KeyboardFocusedElementChanged;

    IVisualElement? KeyboardFocusedElement { get; }

    /// <summary>
    /// Get the element at the specified point.
    /// </summary>
    /// <param name="point">Point in screen pixels.</param>
    /// <param name="mode"></param>
    /// <returns></returns>
    IVisualElement? ElementFromPoint(PixelPoint point, PickElementMode mode = PickElementMode.Element);

    /// <summary>
    /// Get the element under the mouse pointer.
    /// </summary>
    /// <param name="mode"></param>
    /// <returns></returns>
    IVisualElement? ElementFromPointer(PickElementMode mode = PickElementMode.Element);

    /// <summary>
    /// Let the user pick an element from the screen.
    /// </summary>
    /// <returns></returns>
    Task<IVisualElement?> PickElementAsync(PickElementMode mode);
}