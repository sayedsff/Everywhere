namespace Everywhere.Extensions;

public static class MathExtension
{
    public static bool IsCloseTo(this double value, double target, double tolerance = double.Epsilon)
    {
        return Math.Abs(value - target) < tolerance;
    }
}