using System.ComponentModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Attributes;
using Everywhere.I18N;
using Microsoft.Extensions.Configuration;

namespace Everywhere.Models;

public class SettingsBase(string section) : ObservableObject
{
    private static readonly IConfiguration Configuration = ServiceLocator.Resolve<IConfiguration>("settings");

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (section.Length == 0) Configuration.Set(this);
        else Configuration.Set(section, this);
    }
}

[Serializable]
public partial class Settings() : SettingsBase(string.Empty)
{
    [SelectionSettingsItem(PropertyName = nameof(LanguageSource))]
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
    public IEnumerable<string> LanguageSource => LocaleManager.AvailableLocaleNames;
}