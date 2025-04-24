namespace Everywhere.Enums;

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