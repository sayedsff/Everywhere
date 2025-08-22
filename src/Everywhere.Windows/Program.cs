using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Everywhere.Chat;
using Everywhere.Chat.Plugins;
using Everywhere.Database;
using Everywhere.Extensions;
using Everywhere.Initialization;
using Everywhere.Interfaces;
using Everywhere.Models;
using Everywhere.ViewModels;
using Everywhere.Views;
using Everywhere.Views.Pages;
using Everywhere.Windows.ChatPlugins;
using Everywhere.Windows.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace Everywhere.Windows;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Entrance.Initialize(args);

        ServiceLocator.Build(x => x

                #region Basic

                .AddLogging(builder => builder
                    .AddSerilog(dispose: true)
                    .AddFilter<SerilogLoggerProvider>("Microsoft.EntityFrameworkCore", LogLevel.Warning))
                .AddSingleton<IRuntimeConstantProvider, RuntimeConstantProvider>()
                .AddSingleton<IVisualElementContext, Win32VisualElementContext>()
                .AddSingleton<IHotkeyListener, Win32HotkeyListener>()
                .AddSingleton<INativeHelper, Win32NativeHelper>()
                .AddSingleton<ISoftwareUpdater, SoftwareUpdater>()
                .AddSettings()

                #endregion

                #region Avalonia Basic

                .AddDialogManagerAndToastManager()
                .AddTransient<IClipboard>(_ =>
                    Application.Current.As<App>()?.TopLevel.Clipboard ??
                    throw new InvalidOperationException("Clipboard is not available."))
                .AddTransient<IStorageProvider>(_ =>
                    Application.Current.As<App>()?.TopLevel.StorageProvider ??
                    throw new InvalidOperationException("StorageProvider is not available."))
                .AddTransient<ILauncher>(_ =>
                    Application.Current.As<App>()?.TopLevel.Launcher ??
                    throw new InvalidOperationException("Launcher is not available."))

                #endregion

                #region View & ViewModel

                .AddSingleton<VisualTreeDebugger>()
                .AddSingleton<ChatFloatingWindowViewModel>()
                .AddSingleton<ChatFloatingWindow>()
                .AddTransient<IMainViewPageFactory, SettingsCategoryPageFactory>()
                .AddSingleton<ChatPluginPageViewModel>()
                .AddSingleton<IMainViewPage, ChatPluginPage>()
                .AddSingleton<AboutPageViewModel>()
                .AddSingleton<IMainViewPage, AboutPage>()
                .AddSingleton<WelcomeViewModel>()
                .AddSingleton<WelcomeView>()
                .AddSingleton<MainViewModel>()
                .AddSingleton<MainView>()

                #endregion

                #region Database

                .AddChatDbContextAndStorage()

                #endregion

                #region Assistant Chat

                .AddSingleton<IKernelMixinFactory, KernelMixinFactory>()
                .AddSingleton<ChatContextManager>()
                .AddTransient<IChatContextManager>(xx => xx.GetRequiredService<ChatContextManager>())
                .AddSingleton<IChatPluginManager>(xx => new ChatPluginManager().WithBuiltInPlugins(
                    new WebSearchEnginePlugin(
                        xx.GetRequiredService<Settings>().Plugin.WebSearchEngine,
                        xx.GetRequiredService<IRuntimeConstantProvider>(),
                        xx.GetRequiredService<ILoggerFactory>()),
                    new FileSystemPlugin(xx.GetRequiredService<ILogger<FileSystemPlugin>>()),
                    new PowerShellPlugin(xx.GetRequiredService<ILogger<PowerShellPlugin>>())))
                .AddSingleton<IChatService, ChatService>()

                #endregion

                #region Initialize

                .AddTransient<IAsyncInitializer, HotkeyInitializer>()
                .AddTransient<IAsyncInitializer, SettingsInitializer>()
                .AddTransient<IAsyncInitializer, UpdaterInitializer>()
                .AddTransient<IAsyncInitializer>(xx => xx.GetRequiredService<ChatContextManager>())

            #endregion

        );

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}