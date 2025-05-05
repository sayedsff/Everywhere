using System.Text.Json;
using Avalonia;
using Everywhere.Enums;
using Everywhere.Extensions;
using Everywhere.Interfaces;
using Everywhere.Models;
using Everywhere.ViewModels;
using Everywhere.Views;
using Everywhere.Views.Pages;
using Everywhere.Windows.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using SukiUI.Dialogs;
using SukiUI.Toasts;
using WritableJsonConfiguration;

namespace Everywhere.Windows;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        ServiceLocator.Build(x => x

                #region Basic

                .AddSingleton<IRuntimeConstantProvider, RuntimeConstantProvider>()
                .AddKeyedSingleton<IConfiguration>(
                    "settings",
                    (xx, _) =>
                    {
                        var settingsJsonPath = Path.Combine(
                            xx.GetRequiredService<IRuntimeConstantProvider>().Get<string>(RuntimeConstantType.WritableDataPath),
                            "settings.json");
                        try
                        {
                            return WritableJsonConfigurationFabric.Create(settingsJsonPath);
                        }
                        catch (Exception ex) when (ex is JsonException or InvalidDataException)
                        {
                            File.Delete(settingsJsonPath);
                            return WritableJsonConfigurationFabric.Create(settingsJsonPath);
                        }
                    })
                .AddSingleton<Settings>(xx =>
                {
                    var configuration = xx.GetRequiredKeyedService<IConfiguration>("settings");
                    if (configuration.Get<Settings>() is { } settings) return settings;
                    settings = new Settings();
                    configuration.Bind(settings);
                    return settings;
                })
                .AddSingleton<IVisualElementContext, Win32VisualElementContext>()
                .AddSingleton<IUserInputTrigger, Win32UserInputTrigger>()
                .AddSingleton<IPlatformHelper, Win32PlatformHelper>()
                .AddOpenAIChatCompletion(
                    modelId: "gpt-4o",
                    apiKey: Environment.GetEnvironmentVariable("NODIS_API_KEY", EnvironmentVariableTarget.User).NotNull("NODIS_API_KEY is not set")
                )

                #endregion

                #region Avalonia Basic

                .AddSingleton<ISukiDialogManager, SukiDialogManager>()
                .AddSingleton<ISukiToastManager, SukiToastManager>()

                #endregion

                #region View & ViewModel

                .AddSingleton<VisualTreeDebuggerWindowViewModel>()
                .AddSingleton<VisualTreeDebuggerWindow>()
                .AddSingleton<AgentFloatingWindowViewModel>()
                .AddSingleton<AgentFloatingWindow>()
                .AddSingleton<SettingsPageViewModel>()
                .AddSingleton<IMainViewPage, SettingsPage>()
                .AddSingleton<MainViewModel>()
                .AddSingleton<MainView>()

            #endregion

        );

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}