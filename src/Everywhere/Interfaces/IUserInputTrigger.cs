namespace Everywhere.Interfaces;

public interface IUserInputTrigger
{
    public delegate void PointerHotkeyActivatedHandler(PixelPoint point);
    public delegate void KeyboardHotkeyActivatedHandler();

    event PointerHotkeyActivatedHandler PointerHotkeyActivated;
    event KeyboardHotkeyActivatedHandler KeyboardHotkeyActivated;
}