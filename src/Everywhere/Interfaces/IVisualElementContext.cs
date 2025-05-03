namespace Everywhere.Interfaces;

public interface IVisualElementContext
{
    public delegate void KeyboardFocusedElementChangedHandler(IVisualElement? element);

    event KeyboardFocusedElementChangedHandler KeyboardFocusedElementChanged;

    IVisualElement? KeyboardFocusedElement { get; }

    IVisualElement? PointerOverElement { get; }
}