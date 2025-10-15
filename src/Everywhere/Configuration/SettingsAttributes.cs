namespace Everywhere.Configuration;

/// <summary>
/// This attribute is used to mark properties that should not be serialized or displayed in the UI.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class HiddenSettingsItemAttribute : Attribute;

[AttributeUsage(AttributeTargets.Property)]
public class SettingsItemAttribute : Attribute
{
    /// <summary>
    /// Sets a binding path that will be used to determine if this item is visible in the UI.
    /// </summary>
    public string? IsVisibleBindingPath { get; set; }

    /// <summary>
    /// Sets a binding path that will be used to determine if this item is enabled in the UI.
    /// </summary>
    public string? IsEnabledBindingPath { get; set; }
}

[AttributeUsage(AttributeTargets.Property)]
public class SettingsStringItemAttribute : Attribute
{
    public string? Watermark { get; set; }
    public int MaxLength { get; set; } = int.MaxValue;
    public bool IsMultiline { get; set; }
    public bool IsPassword { get; set; }
    public double Height { get; set; } = double.NaN;
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
    /// A binding path to the property that contains the items to select from.
    /// </summary>
    public required string ItemsSourceBindingPath { get; set; }

    /// <summary>
    /// Should look for i18n keys in the items source
    /// </summary>
    public bool I18N { get; set; }

    /// <summary>
    /// An optional key to use for the DataTemplate to display each item.
    /// </summary>
    public object? DataTemplateKey { get; set; }
}

/// <summary>
/// Indicates this Property should generate items
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class SettingsItemsAttribute : Attribute
{
    public bool IsExpanded { get; set; }
}