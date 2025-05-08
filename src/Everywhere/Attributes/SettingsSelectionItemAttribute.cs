namespace Everywhere.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class SettingsStringItemAttribute : Attribute
{
    public string? Watermark { get; set; }
    public int MaxLength { get; set; } = int.MaxValue;
    public bool IsMultiline { get; set; }
    public bool IsPassword { get; set; }
}

[AttributeUsage(AttributeTargets.Property)]
public class SettingsIntegerItemAttribute : Attribute
{
    public int Min { get; set; } = int.MinValue;
    public int Max { get; set; } = int.MaxValue;
}

[AttributeUsage(AttributeTargets.Property)]
public class SettingsDoubleItemAttribute : Attribute
{
    public double Min { get; set; } = double.NegativeInfinity;
    public double Max { get; set; } = double.PositiveInfinity;
    public double Step { get; set; } = 1.0d;
}

[AttributeUsage(AttributeTargets.Property)]
public class SettingsSelectionItemAttribute : Attribute
{
    public required string PropertyName { get; set; }
}