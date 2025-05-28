using System.Text;
using Avalonia.Input;

namespace Everywhere.Models;

public readonly record struct KeyboardHotkey(Key Key, KeyModifiers Modifiers)
{
    public bool IsEmpty => Key == Key.None && Modifiers == KeyModifiers.None;

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
        if (Key != Key.None) sb.Append(Key);
        return sb.ToString();
    }
}