using System.ComponentModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Attributes;
using Everywhere.I18N;
using Microsoft.Extensions.Configuration;

namespace Everywhere.Models;

public class SettingsBase(string section) : ObservableObject
{
    public static IConfiguration? Configuration { get; set; }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (Configuration == null) return;
        if (section.Length == 0) Configuration.Set(this);
        else Configuration.Set(section, this);
    }
}

[Serializable]
public class Settings
{
    public CommonSettings Common { get; init; } = new();

    public BehaviorSettings Behavior { get; init; } = new();

    public ModelSettings Model { get; init; } = new();
}

public partial class CommonSettings() : SettingsBase("Common")
{
    [SettingsSelectionItem(PropertyName = nameof(LanguageSource))]
    public string Language
    {
        get => LocaleManager.CurrentLocale;
        set
        {
            if (LocaleManager.CurrentLocale == value) return;
            LocaleManager.CurrentLocale = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public static IEnumerable<string> LanguageSource => LocaleManager.AvailableLocaleNames;
}

public partial class BehaviorSettings() : SettingsBase("Behavior")
{
    [ObservableProperty]
    public partial bool IsInputAssistantEnabled { get; set; }

    [ObservableProperty]
    public partial bool ShowInputAssistantFloatingWindow { get; set; }

    [ObservableProperty]
    public partial Hotkey InputAssistantHotkey { get; set; }
}

public partial class ModelSettings() : SettingsBase("Model")
{
    [ObservableProperty]
    [SettingsStringItem(Watermark = "gpt-4o")]
    public partial string ModelName { get; set; } = "gpt-4o";

    [ObservableProperty]
    [SettingsStringItem(Watermark = "https://api.openai.com/v1")]
    public partial string Endpoint { get; set; } = "https://api.openai.com/v1";

    [ObservableProperty]
    [SettingsStringItem(Watermark = "sk-xxxxxxxxxxxxxxx")]
    public partial string ApiKey { get; set; } = string.Empty;

    [ObservableProperty]
    [SettingsDoubleItem(Min = 0.0, Max = 2.0, Step = 0.1)]
    public partial double Temperature { get; set; } = 1.0;

    [ObservableProperty]
    [SettingsDoubleItem(Min = 0.0, Max = 1.0, Step = 0.1)]
    public partial double TopP { get; set; } = 1.0;

    [ObservableProperty]
    public partial bool IsImageEnabled { get; set; } = true;

    [ObservableProperty]
    public partial bool IsToolCallEnabled { get; set; } = true;
}