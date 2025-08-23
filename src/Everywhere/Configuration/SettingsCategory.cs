using CommunityToolkit.Mvvm.ComponentModel;
using Lucide.Avalonia;

namespace Everywhere.Configuration;

/// <summary>
/// Represents a category of settings in the application.
/// Each category can have a display name and an icon associated with it.
/// </summary>
[Serializable]
public abstract class SettingsCategory : ObservableObject
{
    /// <summary>
    /// The display name of the settings category.
    /// This is used for I18N, the key is "SettingsCategory_{Header}_Header".
    /// </summary>
    [HiddenSettingsItem]
    public virtual string Header => GetType().Name.TrimEnd("Settings", "SettingsCategory");

    /// <summary>
    /// The Icon of the settings category.
    /// </summary>
    [HiddenSettingsItem]
    public virtual LucideIconKind Icon => 0;
}