using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Attributes;
using Everywhere.Enums;
using Everywhere.Utilities;
using Everywhere.ValueConverters;
using Lucide.Avalonia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShadUI;
using WritableJsonConfiguration;

namespace Everywhere.Models;

/// <summary>
/// Represents the application settings.
/// A singleton that holds all the settings categories.
/// And automatically saves the settings to a JSON file when any setting is changed.
/// </summary>
[Serializable]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public class Settings : ObservableObject
{
    public ModelSettings Model { get; set; } = new();

    public CommonSettings Common { get; set; } = new();

    public BehaviorSettings Behavior { get; set; } = new();

    [HiddenSettingsItem]
    public PluginSettings Plugin { get; set; } = new();

    [HiddenSettingsItem]
    public InternalSettings Internal { get; set; } = new();

    private readonly Dictionary<string, object?> _saveBuffer = new();
    private readonly DebounceExecutor<Dictionary<string, object?>> _saveDebounceExecutor;

    public Settings(IConfiguration configuration)
    {
        _saveDebounceExecutor = new DebounceExecutor<Dictionary<string, object?>>(
            () => _saveBuffer,
            saveBuffer =>
            {
                lock (saveBuffer)
                {
                    if (saveBuffer.Count == 0) return;
                    foreach (var (key, value) in saveBuffer) configuration.Set(key, value);
                    saveBuffer.Clear();
                }
            },
            TimeSpan.FromSeconds(0.5));

        new ObjectObserver(HandleSettingsChanges).Observe(this);

        void HandleSettingsChanges(in ObjectObserverChangedEventArgs e)
        {
            lock (_saveBuffer) _saveBuffer[e.Path] = e.Value;
            _saveDebounceExecutor.Trigger();
        }
    }
}

public partial class CommonSettings : SettingsCategory
{
    private static INativeHelper NativeHelper => ServiceLocator.Resolve<INativeHelper>();
    private static ISoftwareUpdater SoftwareUpdater => ServiceLocator.Resolve<ISoftwareUpdater>();
    private static ILogger Logger => ServiceLocator.Resolve<ILogger<CommonSettings>>();

    public override string Header => "Common";

    public override LucideIconKind Icon => LucideIconKind.Box;

    [ObservableProperty]
    [HiddenSettingsItem]
    public partial DateTimeOffset? LastUpdateCheckTime { get; set; }

    public static StackPanel SoftwareUpdate => new()
    {
        Spacing = 8,
        Children =
        {
            new TextBlock
            {
                Inlines =
                [
                    new Run
                    {
                        [!Run.TextProperty] = new FormattedDynamicResourceKey(
                            LocaleKey.Settings_Common_SoftwareUpdate_TextBlock_Run1_Text,
                            new DirectResourceKey(SoftwareUpdater.CurrentVersion.ToString(3))).ToBinding()
                    },
                    new LineBreak(),
                    new Run
                    {
                        [!Run.TextProperty] = new DynamicResourceKey(LocaleKey.Settings_Common_SoftwareUpdate_TextBlock_Run2_Text).ToBinding()
                    },
                    new Run
                    {
                        [!Run.TextProperty] = new Binding
                        {
                            Path = $"{nameof(Settings.Common)}.{nameof(LastUpdateCheckTime)}",
                            Source = ServiceLocator.Resolve<Settings>(),
                            Converter = CommonConverters.DateTimeOffsetToString,
                            ConverterParameter = "G",
                            Mode = BindingMode.OneWay
                        }
                    },
                    new LineBreak(),
                    new InlineUIContainer
                    {
                        Child = new Button
                        {
                            Classes = { "Ghost" },
                            Content = new TextBlock
                            {
                                Inlines =
                                [
                                    new Run
                                    {
                                        TextDecorations = TextDecorations.Underline,
                                        [!TextElement.ForegroundProperty] = new DynamicResourceExtension("InfoColor"),
                                        [!Run.TextProperty] = new DynamicResourceKey(
                                            LocaleKey.Settings_Common_SoftwareUpdate_TextBlock_ReleaseNotes_Text).ToBinding()
                                    }
                                ]
                            },
                            Command = new AsyncRelayCommand(() =>
                                ServiceLocator.Resolve<ILauncher>()
                                    .LaunchUriAsync(new Uri("https://github.com/DearVa/Everywhere/releases", UriKind.Absolute))
                            ),
                            CornerRadius = new CornerRadius(1),
                            Height = double.NaN,
                            MinHeight = 0,
                            Padding = new Thickness(),
                        }
                    }
                ],
                VerticalAlignment = VerticalAlignment.Center,
            },
            new Button
            {
                [!ButtonAssist.ShowProgressProperty] = new Binding
                {
                    Path = "Command.IsRunning",
                    RelativeSource = new RelativeSource(RelativeSourceMode.Self)
                },
                [!ContentControl.ContentProperty] =
                    new DynamicResourceKey(LocaleKey.Settings_Common_SoftwareUpdate_CheckForUpdatesButton_Content).ToBinding(),
                [!Visual.IsVisibleProperty] = new Binding
                {
                    Path = $"!{nameof(ISoftwareUpdater.LatestVersion)}",
                    Source = SoftwareUpdater
                },
                Classes = { "Outline" },
                Command = new AsyncRelayCommand(async () =>
                {
                    var softwareUpdater = SoftwareUpdater;

                    try
                    {
                        await softwareUpdater.CheckForUpdatesAsync();

                        var toastMessage = softwareUpdater.LatestVersion is null ?
                            new DynamicResourceKey(LocaleKey.Settings_Common_SoftwareUpdate_Toast_AlreadyLatestVersion) :
                            new FormattedDynamicResourceKey(
                                LocaleKey.Settings_Common_SoftwareUpdate_Toast_NewVersionFound,
                                new DirectResourceKey(softwareUpdater.LatestVersion));
                        ServiceLocator.Resolve<ToastManager>()
                            .CreateToast(LocaleKey.Common_Info.I18N())
                            .WithContent(toastMessage)
                            .DismissOnClick()
                            .OnBottomRight()
                            .ShowInfo();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to check for updates.");
                        ShowErrorToast(ex);
                    }
                }),
                Margin = new Thickness(32, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            },
            new Button
            {
                [!ButtonAssist.ShowProgressProperty] = new Binding
                {
                    Path = "Command.IsRunning",
                    RelativeSource = new RelativeSource(RelativeSourceMode.Self)
                },
                [!TemplatedControl.BackgroundProperty] = new DynamicResourceExtension("SuccessColor60"),
                [!Visual.IsVisibleProperty] = new Binding
                {
                    Path = $"!!{nameof(ISoftwareUpdater.LatestVersion)}",
                    Source = SoftwareUpdater
                },
                Classes = { "Outline" },
                Content = new TextBlock
                {
                    Inlines =
                    [
                        new Run
                        {
                            [!Run.TextProperty] = new DynamicResourceKey(LocaleKey.Settings_Common_SoftwareUpdate_PerformUpdateButton_Content)
                                .ToBinding(),
                        },
                        new Run
                        {
                            [!Run.TextProperty] = new Binding
                            {
                                Path = nameof(ISoftwareUpdater.LatestVersion),
                                Source = SoftwareUpdater
                            }
                        }
                    ]
                },
                Command = new AsyncRelayCommand(async () =>
                {
                    try
                    {
                        var progress = new Progress<double>();
                        ServiceLocator.Resolve<ToastManager>()
                            .CreateToast(LocaleKey.Common_Info.I18N())
                            .WithContent(LocaleKey.Settings_Common_SoftwareUpdate_Toast_DownloadingUpdate.I18N())
                            .WithProgress(progress)
                            .WithDelay(0d)
                            .OnBottomRight()
                            .ShowInfo();
                        await SoftwareUpdater.PerformUpdateAsync(progress);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to perform update.");
                        ShowErrorToast(ex);
                    }
                }),
                VerticalAlignment = VerticalAlignment.Center,
            },
        },
        Orientation = Orientation.Horizontal
    };

    [ObservableProperty]
    public partial bool IsAutomaticUpdateCheckEnabled { get; set; } = true;

    [HiddenSettingsItem]
    public static IEnumerable<string> LanguageSource => LocaleManager.AvailableLocaleNames;

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
    public static Button RestartAsAdministrator => new()
    {
        Classes = { "Outline" },
        [!ContentControl.ContentProperty] = new DynamicResourceKey(LocaleKey.Settings_Common_RestartAsAdministrator_Button_Content).ToBinding(),
        Command = new RelayCommand(() =>
        {
            try
            {
                NativeHelper.RestartAsAdministrator();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to restart as administrator.");
                ShowErrorToast(ex);
            }
        }),
        HorizontalAlignment = HorizontalAlignment.Left,
        [ButtonAssist.IconProperty] = new LucideIcon
        {
            Kind = LucideIconKind.Shield,
            Size = 18,
            Width = 18,
            Height = 18,
            Margin = new Thickness(0, 0, 6, 0)
        },
    };

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

    private static void ShowErrorToast(Exception ex) => ServiceLocator.Resolve<ToastManager>()
        .CreateToast(LocaleKey.Common_Error.I18N())
        .WithContent(ex.GetFriendlyMessage())
        .DismissOnClick()
        .OnBottomRight()
        .ShowError();
}

public partial class BehaviorSettings : SettingsCategory
{
    public override string Header => "Behavior";

    public override LucideIconKind Icon => LucideIconKind.Keyboard;

    [ObservableProperty]
    public partial KeyboardHotkey ChatHotkey { get; set; } = new(Key.E, KeyModifiers.Control | KeyModifiers.Shift);

    [ObservableProperty]
    public partial ChatFloatingWindowPinMode ChatFloatingWindowPinMode { get; set; }
}

public partial class ModelSettings : SettingsCategory
{
    public override string Header => "Model";

    public override LucideIconKind Icon => LucideIconKind.Brain;

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
    [SettingsSelectionItem(ItemsSourceBindingPath = nameof(ModelProviders), DataTemplateKey = typeof(ModelProvider))]
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
    [SettingsSelectionItem(
        ItemsSourceBindingPath = $"{nameof(SelectedModelProvider)}.{nameof(ModelProvider.ModelDefinitions)}",
        DataTemplateKey = typeof(ModelDefinition))]
    public ModelDefinition? SelectedModelDefinition
    {
        get => SelectedModelProvider?.ModelDefinitions.FirstOrDefault(m => m.Id == SelectedModelDefinitionId);
        set => SelectedModelDefinitionId = value?.Id;
    }

    [ObservableProperty]
    [SettingsDoubleItem(Min = 0.0, Max = 2.0, Step = 0.1)]
    public partial Customizable<double> Temperature { get; set; } = 1.0;

    [ObservableProperty]
    [SettingsDoubleItem(Min = 0.0, Max = 1.0, Step = 0.1)]
    public partial Customizable<double> TopP { get; set; } = 0.9;

    [ObservableProperty]
    [SettingsDoubleItem(Min = -2.0, Max = 2.0, Step = 0.1)]
    public partial Customizable<double> PresencePenalty { get; set; } = 0.0;

    [ObservableProperty]
    [SettingsDoubleItem(Min = -2.0, Max = 2.0, Step = 0.1)]
    public partial Customizable<double> FrequencyPenalty { get; set; } = 0.0;
}

public class PluginSettings : SettingsCategory
{
    public override string Header => "Plugin";

    public WebSearchEngineSettings WebSearchEngine { get; set; } = new();
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