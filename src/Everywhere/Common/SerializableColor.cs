using Avalonia.Media;

namespace Everywhere.Common;

public struct SerializableColor
{
    public byte A { get; set; }
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }

    public static implicit operator SerializableColor(Color color) => new()
    {
        A = color.A,
        R = color.R,
        G = color.G,
        B = color.B
    };

    public static implicit operator Color(SerializableColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);
}