using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.AI;
using Everywhere.Chat;
using Everywhere.Chat.Plugins;
using Everywhere.Common;
using Everywhere.Initialization;
using Everywhere.Interop;
using Everywhere.Views.Configuration;
using Lucide.Avalonia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ObservableCollections;
using ShadUI;
using WritableJsonConfiguration;

namespace Everywhere.Configuration;

/// <summary>
/// Represents the application settings.
/// A singleton that holds all the settings categories.
/// And automatically saves the settings to a JSON file when any setting is changed.
/// </summary>
[Serializable]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public class Settings : ObservableObject
{
    public CommonSettings Common { get; set; } = new();

    [HiddenSettingsItem]
    public ModelSettings Model { get; set; } = new();

    public ChatWindowSettings ChatWindow { get; set; } = new();

    [HiddenSettingsItem]
    public PluginSettings Plugin { get; set; } = new();

    [HiddenSettingsItem]
    public InternalSettings Internal { get; set; } = new();
}

public partial class CommonSettings : SettingsCategory
{
    private static INativeHelper NativeHelper => ServiceLocator.Resolve<INativeHelper>();
    private static ILogger Logger => ServiceLocator.Resolve<ILogger<CommonSettings>>();

    public override string Header => "Common";

    public override LucideIconKind Icon => LucideIconKind.Box;

    [ObservableProperty]
    [HiddenSettingsItem]
    public partial DateTimeOffset? LastUpdateCheckTime { get; set; }

    [JsonIgnore]
    public SettingsControl<SoftwareUpdateControl> SoftwareUpdate { get; } = new();

    [ObservableProperty]
    public partial bool IsAutomaticUpdateCheckEnabled { get; set; } = true;

    [HiddenSettingsItem]
    public static IEnumerable<string> LanguageSource => LocaleManager.AvailableLocaleNames;

    /// <summary>
    /// Gets or sets the current application language.
    /// </summary>
    /// <remarks>
    /// Warn that this may be "default", which stands for en-US.
    /// </remarks>
    /// <example>
    /// default, zh-hans, ru, de, ja, it, fr, es, ko, zh-hant, zh-hant-hk
    /// </example>
    [SettingsSelectionItem(ItemsSourceBindingPath = nameof(LanguageSource), I18N = true)]
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

    [HiddenSettingsItem]
    public static IEnumerable<string> ThemeSource => ["System", "Dark", "Light"];

    [ObservableProperty]
    [SettingsSelectionItem(ItemsSourceBindingPath = nameof(ThemeSource), I18N = true)]
    public partial string Theme { get; set; } = ThemeSource.First();

    [JsonIgnore]
    [HiddenSettingsItem]
    public static bool IsAdministrator => NativeHelper.IsAdministrator;

    [JsonIgnore]
    [SettingsItem(IsVisibleBindingPath = $"!{nameof(IsAdministrator)}")]
    public SettingsControl<RestartAsAdministratorControl> RestartAsAdministrator { get; } = new();

    [JsonIgnore]
    [SettingsItem(IsEnabledBindingPath = $"{nameof(IsAdministrator)} || !{nameof(IsAdministratorStartupEnabled)}")]
    public bool IsStartupEnabled
    {
        get => NativeHelper.IsUserStartupEnabled || NativeHelper.IsAdministratorStartupEnabled;
        set
        {
            try
            {
                // If disabling user startup while admin startup is enabled, also disable admin startup.
                if (!value && NativeHelper.IsAdministratorStartupEnabled)
                {
                    if (IsAdministrator)
                    {
                        NativeHelper.IsAdministratorStartupEnabled = false;
                        OnPropertyChanged(nameof(IsAdministratorStartupEnabled));
                    }
                    else
                    {
                        return;
                    }
                }

                NativeHelper.IsUserStartupEnabled = value;
                OnPropertyChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to set user startup enabled.");
                ShowErrorToast(ex);
            }
        }
    }

    [JsonIgnore]
    [SettingsItem(IsVisibleBindingPath = nameof(IsStartupEnabled), IsEnabledBindingPath = nameof(IsAdministrator))]
    public bool IsAdministratorStartupEnabled
    {
        get => NativeHelper.IsAdministratorStartupEnabled;
        set
        {
            try
            {
                if (!IsAdministrator) return;

                // If enabling admin startup while user startup is disabled, also enable user startup.
                NativeHelper.IsUserStartupEnabled = !value;
                NativeHelper.IsAdministratorStartupEnabled = value;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to set administrator startup enabled.");
                ShowErrorToast(ex);
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsStartupEnabled));
        }
    }

    public bool DiagnosticData
    {
        get => !Entrance.SendOnlyNecessaryTelemetry;
        set
        {
            Entrance.SendOnlyNecessaryTelemetry = !value;
            OnPropertyChanged();
        }
    }

    private static void ShowErrorToast(Exception ex) => ServiceLocator.Resolve<ToastManager>()
        .CreateToast(LocaleKey.Common_Error.I18N())
        .WithContent(ex.GetFriendlyMessage())
        .DismissOnClick()
        .OnBottomRight()
        .ShowError();
}

public partial class ChatWindowSettings : SettingsCategory
{
    public override string Header => "ChatWindow";

    public override LucideIconKind Icon => LucideIconKind.MessageCircle;

    [ObservableProperty]
    public partial KeyboardHotkey Hotkey { get; set; } = new(Key.E, KeyModifiers.Control | KeyModifiers.Shift);

    [ObservableProperty]
    public partial ChatWindowPinMode WindowPinMode { get; set; }

    [ObservableProperty]
    public partial VisualTreeXmlDetailLevel VisualTreeXmlDetailLevel { get; set; } = VisualTreeXmlDetailLevel.Detailed;

    /// <summary>
    /// When enabled, automatically add focused element as attachment when opening chat window.
    /// </summary>
    [ObservableProperty]
    public partial bool AutomaticallyAddElement { get; set; } = true;

    /// <summary>
    /// When enabled, chat window can generate response in the background when closed.
    /// </summary>
    [ObservableProperty]
    public partial bool AllowRunInBackground { get; set; } = true;

    /// <summary>
    /// When enabled, show chat statistics in the chat window.
    /// </summary>
    [ObservableProperty]
    public partial bool ShowChatStatistics { get; set; } = true;

    // [ObservableProperty]
    // [SettingsSelectionItem(ItemsSourceBindingPath = "")]
    // public partial Guid TitleGeneratorAssistantId { get; set; }
    //
    // [ObservableProperty]
    // [SettingsStringItem(Watermark = Prompts.TitleGeneratorPrompt, IsMultiline = true, Height = 50)]
    // public partial Customizable<string> TitleGeneratorPromptTemplate { get; set; } = Prompts.TitleGeneratorPrompt;
}

public partial class ModelSettings : SettingsCategory
{
    public override string Header => "Model";

    public override LucideIconKind Icon => LucideIconKind.Brain;

    [ObservableProperty]
    public partial ObservableCollection<CustomAssistant> CustomAssistants { get; set; } = [];

    [ObservableProperty]
    public partial Guid SelectedCustomAssistantId { get; set; }

    /// <summary>
    /// Gets or sets the currently selected custom assistant via <see cref="SelectedCustomAssistantId"/>.
    /// If the index is invalid, returns the first assistant or null if the list is empty.
    /// Setting this property will update the index accordingly.
    /// </summary>
    [JsonIgnore]
    public CustomAssistant? SelectedCustomAssistant
    {
        get => CustomAssistants.FirstOrDefault(a => a.Id == SelectedCustomAssistantId) ??
            CustomAssistants.FirstOrDefault(); // Default to first assistant if index is invalid.
        set
        {
            SelectedCustomAssistantId = CustomAssistants.FirstOrDefault(a => a == value)?.Id ?? Guid.Empty;
            OnPropertyChanged();
        }
    }
}

public class PluginSettings : SettingsCategory
{
    public override string Header => "Plugin";

    public ObservableDictionary<string, bool> IsEnabledRecords { get; set; } = new();

    public WebSearchEngineSettings WebSearchEngine { get; set; } = new();

    public PluginSettings()
    {
        IsEnabledRecords.CollectionChanged += delegate { OnPropertyChanged(nameof(IsEnabledRecords)); };
    }
}

public partial class WebSearchEngineSettings : SettingsCategory
{
    [ObservableProperty]
    public partial ObservableCollection<WebSearchEngineProvider> WebSearchEngineProviders { get; set; } = [];

    [HiddenSettingsItem]
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedWebSearchEngineProvider))]
    public partial string? SelectedWebSearchEngineProviderId { get; set; }

    [JsonIgnore]
    [SettingsItems(IsExpanded = true)]
    [SettingsSelectionItem(
        ItemsSourceBindingPath = nameof(WebSearchEngineProviders),
        DataTemplateKey = typeof(WebSearchEngineProvider))]
    public WebSearchEngineProvider? SelectedWebSearchEngineProvider
    {
        get => WebSearchEngineProviders.FirstOrDefault(p => p.Id == SelectedWebSearchEngineProviderId);
        set
        {
            if (Equals(SelectedWebSearchEngineProviderId, value?.Id)) return;
            SelectedWebSearchEngineProviderId = value?.Id;
        }
    }
}

public partial class InternalSettings : SettingsCategory
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
    public partial bool IsToolCallEnabled { get; set; }

    public int MaxChatAttachmentCount { get; set; } = 10;

    [ObservableProperty]
    public partial bool IsMainViewSidebarExpanded { get; set; }

    [ObservableProperty]
    public partial bool IsChatWindowPinned { get; set; }

    [ObservableProperty]
    public partial string? ChatInputBoxText { get; set; }

    [ObservableProperty]
    public partial int VisualTreeTokenLimit { get; set; } = 4096;
}

public static class SettingsExtensions
{
    public static IServiceCollection AddSettings(this IServiceCollection services) => services
        .AddKeyedSingleton<IConfiguration>(
            typeof(Settings),
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
            var configuration = xx.GetRequiredKeyedService<IConfiguration>(typeof(Settings));
            var settings = new Settings();
            configuration.Bind(settings);
            return settings;
        })
        .AddTransient<SoftwareUpdateControl>()
        .AddTransient<RestartAsAdministratorControl>()
        .AddTransient<IAsyncInitializer, SettingsInitializer>();
}