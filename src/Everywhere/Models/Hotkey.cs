using System.Text;
using Avalonia.Input;

namespace Everywhere.Models;

public readonly record struct Hotkey(Key Key, KeyModifiers Modifiers)
{
    public override string ToString()
    {
#if IsOSX
        const string control = "⌃";
        const string shift = "⇧";
        const string alt = "⌥";
        const string meta = "⌘";
#else
        const string control = "Ctrl+";
        const string shift = "Shift+";
        const string alt = "Alt+";
        const string meta = "Win+";

        if (Modifiers == (KeyModifiers.Shift | KeyModifiers.Meta) && Key == Key.F23)
        {
            return "Copilot";
        }
#endif

        var sb = new StringBuilder();
        if (Modifiers.HasFlag(KeyModifiers.Control)) sb.Append(control);
        if (Modifiers.HasFlag(KeyModifiers.Shift)) sb.Append(shift);
        if (Modifiers.HasFlag(KeyModifiers.Alt)) sb.Append(alt);
        if (Modifiers.HasFlag(KeyModifiers.Meta)) sb.Append(meta);
        sb.Append(Key);
        return sb.ToString();
    }
}