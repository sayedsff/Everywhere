namespace Everywhere.Attributes;

/// <summary>
/// This attribute is used to mark properties that should not be serialized or displayed in the UI.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field)]
public class HiddenSettingsItemAttribute : Attribute
{
    public string? Condition { get; set; }
}

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
    public bool IsSliderVisible { get; set; } = true;
}

[AttributeUsage(AttributeTargets.Property)]
public class SettingsDoubleItemAttribute : Attribute
{
    public double Min { get; set; } = double.NegativeInfinity;
    public double Max { get; set; } = double.PositiveInfinity;
    public double Step { get; set; } = 1.0d;
    public bool IsSliderVisible { get; set; } = true;
}

[AttributeUsage(AttributeTargets.Property)]
public class SettingsSelectionItemAttribute : Attribute
{
    /// <summary>
    /// this can be a binding path
    /// </summary>
    public required string ItemsSource { get; set; }

    /// <summary>
    /// Should look for i18n keys in the items source
    /// </summary>
    public bool I18N { get; set; }
}

/// <summary>
/// Indicates this Property should generate items
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class SettingsItemsAttribute : Attribute
{
    public bool IsExpanded { get; set; }
}