using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Everywhere.AI;
using Everywhere.Chat;
using Everywhere.Chat.Plugins;
using Everywhere.Common;
using Everywhere.Configuration;
using Everywhere.Extensions;
using Everywhere.Initialization;
using Everywhere.Interop;
using Everywhere.ViewModels;
using Everywhere.Views;
using Everywhere.Views.Configuration;
using Everywhere.Views.Pages;
using Everywhere.Windows.Chat.Plugins;
using Everywhere.Windows.Configuration;
using Everywhere.Windows.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using SoftwareUpdater = Everywhere.Windows.Common.SoftwareUpdater;

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
                .AddWatchdogManager()

                #endregion

                .AddAvaloniaBasicServices()
                .AddViewsAndViewModels()
                .AddDatabaseAndStorage()

                #region Chat Plugins

                .AddTransient<BuiltInChatPlugin, VisualTreePlugin>()
                .AddTransient<BuiltInChatPlugin, WebSearchEnginePlugin>()
                .AddTransient<BuiltInChatPlugin, FileSystemPlugin>()
                .AddTransient<BuiltInChatPlugin, PowerShellPlugin>()
                .AddTransient<BuiltInChatPlugin, WindowsSystemApiPlugin>()

                #endregion

                #region Chat

                .AddSingleton<IKernelMixinFactory, KernelMixinFactory>()
                .AddSingleton<IChatPluginManager, ChatPluginManager>()
                .AddSingleton<IChatService, ChatService>()
                .AddChatContextManager()

                #endregion

                #region Initialize

                .AddTransient<IAsyncInitializer, HotkeyInitializer>()
                .AddTransient<IAsyncInitializer, UpdaterInitializer>()

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