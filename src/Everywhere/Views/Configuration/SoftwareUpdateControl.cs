using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Common;
using Everywhere.Configuration;
using Everywhere.ValueConverters;
using Lucide.Avalonia;
using Microsoft.Extensions.Logging;
using ShadUI;

namespace Everywhere.Views.Configuration;

/// <summary>
/// A control that provides software update functionality used in Common settings.
/// </summary>
public class SoftwareUpdateControl : StackPanel
{
    public SoftwareUpdateControl(
        Settings settings,
        ISoftwareUpdater softwareUpdater,
        ToastManager toastManager,
        ILogger<SoftwareUpdateControl> logger)
    {
        Spacing = 8;
        Orientation = Orientation.Horizontal;
        Children.AddRange(
        [
            new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        [!TextBlock.TextProperty] = new FormattedDynamicResourceKey(
                                LocaleKey.Settings_Common_SoftwareUpdate_TextBlock_Run1_Text,
                                new DirectResourceKey(softwareUpdater.CurrentVersion.ToString(3)))
                            .ToBinding()
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Children =
                        {
                            new TextBlock
                            {
                                [!TextBlock.TextProperty] =
                                    new DynamicResourceKey(LocaleKey.Settings_Common_SoftwareUpdate_TextBlock_Run2_Text).ToBinding()
                            },
                            new TextBlock
                            {
                                [!TextBlock.TextProperty] = new Binding
                                {
                                    Path = $"{nameof(Settings.Common)}.{nameof(Settings.Common.LastUpdateCheckTime)}",
                                    Source = settings,
                                    Converter = CommonConverters.DateTimeOffsetToString,
                                    ConverterParameter = "G",
                                    Mode = BindingMode.OneWay
                                }
                            },
                        }
                    },
                    new Button
                    {
                        Classes = { "Ghost" },
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Content = new TextBlock
                        {
                            Inlines =
                            [
                                new Run
                                {
                                    TextDecorations = TextDecorations.Underline,
                                    [!TextElement.ForegroundProperty] = new DynamicResourceExtension("InfoColor"),
                                    [!Run.TextProperty] = new DynamicResourceKey(
                                            LocaleKey.Settings_Common_SoftwareUpdate_TextBlock_ReleaseNotes_Text)
                                        .ToBinding()
                                }
                            ]
                        },
                        Command = new AsyncRelayCommand(() =>
                            ServiceLocator.Resolve<ILauncher>()
                                .LaunchUriAsync(
                                    new Uri(
                                        "https://github.com/DearVa/Everywhere/releases",
                                        UriKind.Absolute))
                        ),
                        CornerRadius = new CornerRadius(1),
                        Height = double.NaN,
                        MinHeight = 0,
                        Padding = new Thickness(),
                    },
                }
            },
            new Button
            {
                [ButtonAssist.IconProperty] = new LucideIcon
                {
                    Kind = LucideIconKind.RefreshCcw,
                    Size = 18,
                    Margin = new Thickness(0, 0, 6, 0)
                },
                [!ButtonAssist.ShowProgressProperty] = new Binding
                {
                    Path = "Command.IsRunning",
                    RelativeSource = new RelativeSource(RelativeSourceMode.Self)
                },
                [!ContentControl.ContentProperty] =
                    new DynamicResourceKey(LocaleKey.Settings_Common_SoftwareUpdate_CheckForUpdatesButton_Content).ToBinding(),
                [!IsVisibleProperty] = new Binding
                {
                    Path = $"!{nameof(ISoftwareUpdater.LatestVersion)}",
                    Source = softwareUpdater
                },
                Classes = { "Outline" },
                Command = new AsyncRelayCommand(async () =>
                {
                    try
                    {
                        await softwareUpdater.CheckForUpdatesAsync();

                        var toastMessage = softwareUpdater.LatestVersion is null ?
                            new DynamicResourceKey(LocaleKey.Settings_Common_SoftwareUpdate_Toast_AlreadyLatestVersion) :
                            new FormattedDynamicResourceKey(
                                LocaleKey.Settings_Common_SoftwareUpdate_Toast_NewVersionFound,
                                new DirectResourceKey(softwareUpdater.LatestVersion));
                        toastManager
                            .CreateToast(LocaleKey.Common_Info.I18N())
                            .WithContent(toastMessage)
                            .DismissOnClick()
                            .OnBottomRight()
                            .ShowInfo();
                    }
                    catch (Exception ex)
                    {
                        ex = new HandledException(ex, LocaleKey.Settings_Common_SoftwareUpdate_Toast_CheckForUpdatesFailed_Content);
                        logger.LogError(ex, "Failed to check for updates.");
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
                [!IsVisibleProperty] = new Binding
                {
                    Path = nameof(ISoftwareUpdater.LatestVersion),
                    Source = softwareUpdater,
                    Converter = ObjectConverters.IsNotNull
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
                                Source = softwareUpdater
                            }
                        }
                    ]
                },
                Command = new AsyncRelayCommand(async () =>
                {
                    try
                    {
                        var progress = new Progress<double>();
                        var cts = new CancellationTokenSource();
                        toastManager
                            .CreateToast(LocaleKey.Common_Info.I18N())
                            .WithContent(LocaleKey.Settings_Common_SoftwareUpdate_Toast_DownloadingUpdate.I18N())
                            .WithProgress(progress)
                            .WithCancellationTokenSource(cts)
                            .WithDelay(0d)
                            .OnBottomRight()
                            .ShowInfo();
                        await softwareUpdater.PerformUpdateAsync(progress, cts.Token);
                    }
                    catch (Exception ex)
                    {
                        ex = new HandledException(ex, LocaleKey.Settings_Common_SoftwareUpdate_Toast_UpdateFailed_Content);
                        logger.LogError(ex, "Failed to perform update.");
                        ShowErrorToast(ex);
                    }
                }),
                VerticalAlignment = VerticalAlignment.Center,
            }
        ]);

        void ShowErrorToast(Exception ex) => toastManager
            .CreateToast(LocaleKey.Common_Error.I18N())
            .WithContent(ex.GetFriendlyMessage())
            .DismissOnClick()
            .OnBottomRight()
            .ShowError();
    }
}