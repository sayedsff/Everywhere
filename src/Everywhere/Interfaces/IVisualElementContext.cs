namespace Everywhere.Interfaces;

public interface IVisualElementContext
{
    IVisualElement? KeyboardFocusedElement { get; }

    IVisualElement? PointerOverElement { get; }
}