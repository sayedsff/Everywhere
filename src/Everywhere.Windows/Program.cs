using System.Text.Json;
using Avalonia;
using Everywhere.Enums;
using Everywhere.Extensions;
using Everywhere.Initialization;
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
        ServiceLocator.Build(
            x => x

                #region Basic

                .AddSingleton<IRuntimeConstantProvider, RuntimeConstantProvider>()
                .AddSingleton<Settings>(
                    xx =>
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

                        if (configuration.Get<Settings>() is not { } settings)
                        {
                            settings = new Settings();
                            configuration.Bind(settings);
                        }

                        // set Configuration after binding, or it will save json when init and the values will be incorrect
                        SettingsBase.Configuration = configuration;
                        return settings;
                    })
                .AddSingleton<IVisualElementContext, Win32VisualElementContext>()
                .AddSingleton<IUserInputTrigger, Win32UserInputTrigger>()
                .AddSingleton<IPlatformHelper, Win32PlatformHelper>()

                #endregion

                #region Avalonia Basic

                .AddSingleton<ISukiDialogManager, SukiDialogManager>()
                .AddSingleton<ISukiToastManager, SukiToastManager>()

                #endregion

                #region View & ViewModel

                .AddSingleton<VisualTreeDebuggerWindowViewModel>()
                .AddSingleton<VisualTreeDebuggerWindow>()
                .AddSingleton<AssistantFloatingWindowViewModel>()
                .AddSingleton<AssistantFloatingWindow>()
                .AddSingleton<SettingsPageViewModel>()
                .AddSingleton<IMainViewPage, SettingsPage>()
                .AddSingleton<MainViewModel>()
                .AddSingleton<MainView>()

                #endregion

                #region Initialize

                .AddSingleton<IAsyncInitializer, AssistantInitializer>()

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