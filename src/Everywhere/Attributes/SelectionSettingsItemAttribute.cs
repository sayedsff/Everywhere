namespace Everywhere.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class SelectionSettingsItemAttribute : Attribute
{
    public required string PropertyName { get; set; }
}