using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Attributes;
using Everywhere.Enums;
using Everywhere.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WritableJsonConfiguration;

namespace Everywhere.Models;

public abstract class SettingsBase : ObservableObject;

[Serializable]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public class Settings : ObservableObject
{
    public CommonSettings Common { get; } = new();

    public BehaviorSettings Behavior { get; } = new();

    public ModelSettings Model { get; } = new();

    [HiddenSettingsItem]
    public InternalSettings Internal { get; } = new();

    private readonly DebounceHelper saveDebounceHelper = new(TimeSpan.FromSeconds(0.5));
    private readonly Dictionary<string, object?> saveBuffer = new();

    public Settings(IConfiguration configuration)
    {
        new ObjectObserver(HandleSettingsChanges)
            .Observe(Common, nameof(Common))
            .Observe(Behavior, nameof(Behavior))
            .Observe(Model, nameof(Model))
            .Observe(Internal, nameof(Internal));

        void HandleSettingsChanges(in ObjectObserverChangedEventArgs e)
        {
            lock (saveBuffer) saveBuffer[e.Path] = e.Value;

            saveDebounceHelper.Execute(() =>
            {
                lock (saveBuffer)
                {
                    if (saveBuffer.Count == 0) return;
                    foreach (var (key, value) in saveBuffer) configuration.Set(key, value);
                    saveBuffer.Clear();
                }
            });
        }
    }
}

public partial class CommonSettings : SettingsBase
{
    [SettingsSelectionItem(ItemsSource = nameof(LanguageSource), I18N = true)]
    public string Language
    {
        get
        {
            var currentLocale = LocaleManager.CurrentLocale;
            if (currentLocale is not null) return currentLocale;

            var cultureInfo = CultureInfo.CurrentUICulture;
            while (!string.IsNullOrEmpty(cultureInfo.Name))
            {
                var nameLowered = cultureInfo.Name.ToLower();
                if (LocaleManager.AvailableLocaleNames.Contains(nameLowered))
                {
                    currentLocale = nameLowered;
                    break;
                }

                cultureInfo = cultureInfo.Parent;
            }

            return LocaleManager.CurrentLocale = currentLocale ?? "default";
        }
        set
        {
            if (LocaleManager.CurrentLocale == value) return;
            LocaleManager.CurrentLocale = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public static IEnumerable<string> LanguageSource => LocaleManager.AvailableLocaleNames;

    [ObservableProperty]
    [SettingsSelectionItem(ItemsSource = nameof(ThemeSource), I18N = true)]
    public partial string Theme { get; set; } = ThemeSource.First();

    public static IEnumerable<string> ThemeSource => ["System", "Dark", "Light"];
}

public partial class BehaviorSettings : SettingsBase
{
    [ObservableProperty]
    public partial KeyboardHotkey ChatHotkey { get; set; } = new(Key.E, KeyModifiers.Control | KeyModifiers.Shift);

    [ObservableProperty]
    public partial ChatFloatingWindowPinMode ChatFloatingWindowPinMode { get; set; }
}

public partial class ModelSettings : SettingsBase
{
    [HiddenSettingsItem]
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedModelProvider), nameof(SelectedModelDefinition))]
    public partial ObservableCollection<ModelProvider> ModelProviders { get; set; } = [];

    [HiddenSettingsItem]
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedModelProvider))]
    public partial string? SelectedModelProviderId { get; set; }

    [JsonIgnore]
    [SettingsItems(IsExpanded = true)]
    [SettingsSelectionItem(ItemsSource = nameof(ModelProviders))]
    public ModelProvider? SelectedModelProvider
    {
        get => ModelProviders.FirstOrDefault(p => p.Id == SelectedModelProviderId);
        set
        {
            if (Equals(SelectedModelProviderId, value?.Id)) return;
            SelectedModelProviderId = value?.Id;
            SelectedModelDefinitionId = value?.ModelDefinitions.FirstOrDefault()?.Id;
        }
    }

    [HiddenSettingsItem]
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedModelDefinition))]
    public partial string? SelectedModelDefinitionId { get; set; }

    [JsonIgnore]
    [SettingsItems]
    [SettingsSelectionItem(ItemsSource = $"{nameof(SelectedModelProvider)}.{nameof(ModelProvider.ModelDefinitions)}")]
    public ModelDefinition? SelectedModelDefinition
    {
        get => SelectedModelProvider?.ModelDefinitions.FirstOrDefault(m => m.Id == SelectedModelDefinitionId);
        set => SelectedModelDefinitionId = value?.Id;
    }

    [ObservableProperty]
    [SettingsDoubleItem(Min = 0.0, Max = 2.0, Step = 0.1)]
    public partial double Temperature { get; set; } = 1.0;

    [ObservableProperty]
    [SettingsDoubleItem(Min = 0.0, Max = 1.0, Step = 0.1)]
    public partial double TopP { get; set; } = 0.9;

    [ObservableProperty]
    [SettingsDoubleItem(Min = -2.0, Max = 2.0, Step = 0.1)]
    public partial double PresencePenalty { get; set; } = 0.0;

    [ObservableProperty]
    [SettingsDoubleItem(Min = -2.0, Max = 2.0, Step = 0.1)]
    public partial double FrequencyPenalty { get; set; } = 0.0;

    [ObservableProperty]
    [SettingsSelectionItem(ItemsSource = nameof(WebSearchProviders))]
    public partial string WebSearchProvider { get; set; } = "bing";

    [JsonIgnore]
    public static IEnumerable<string> WebSearchProviders => ["bing", "brave", "bocha"]; // TODO: google

    [ObservableProperty]
    [SettingsStringItem(IsPassword = true)]
    public partial string WebSearchApiKey { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string WebSearchEndpoint { get; set; } = string.Empty;
}

[HiddenSettingsItem]
public partial class InternalSettings : SettingsBase
{
    /// <summary>
    /// Used to popup welcome dialog on first launch and update.
    /// </summary>
    [ObservableProperty]
    public partial string? PreviousLaunchVersion { get; set; }

    /// <summary>
    /// Pop a tray notification when the application is launched for the first time.
    /// </summary>
    [ObservableProperty]
    public partial bool IsFirstTimeHideToTrayIcon { get; set; } = true;

    [ObservableProperty]
    public partial bool IsImageEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsToolCallEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsWebSearchEnabled { get; set; }

    public int MaxChatAttachmentCount { get; set; } = 10;

    [ObservableProperty]
    public partial bool IsMainViewSidebarExpanded { get; set; }

    [ObservableProperty]
    public partial bool IsChatFloatingWindowPinned { get; set; }
}

public static class SettingsExtensions
{
    public static IServiceCollection AddSettings(this IServiceCollection services) => services
        .AddKeyedSingleton<IConfiguration>(
            nameof(Settings),
            (xx, _) =>
            {
                IConfiguration configuration;
                var settingsJsonPath = Path.Combine(
                    xx.GetRequiredService<IRuntimeConstantProvider>().Get<string>(RuntimeConstantType.WritableDataPath),
                    "settings.json");
                try
                {
                    configuration = WritableJsonConfigurationFabric.Create(settingsJsonPath);
                }
                catch (Exception ex) when (ex is JsonException or InvalidDataException)
                {
                    File.Delete(settingsJsonPath);
                    configuration = WritableJsonConfigurationFabric.Create(settingsJsonPath);
                }
                return configuration;
            })
        .AddSingleton<Settings>(xx =>
        {
            var configuration = xx.GetRequiredKeyedService<IConfiguration>(nameof(Settings));
            var settings = new Settings(configuration);
            configuration.Bind(settings);
            return settings;
        });
}