using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Everywhere.Chat;
using Everywhere.Database;
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
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.AI;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.TextGeneration;
using ShadUI;
using WritableJsonConfiguration;

namespace Everywhere.Windows;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        ServiceLocator.Build(x => x

                #region Basic

                .AddSingleton(typeof(ILogger<>), typeof(ConsoleLogger<>))
                .AddSingleton<IRuntimeConstantProvider, RuntimeConstantProvider>()
                .AddSingleton<IVisualElementContext, Win32VisualElementContext>()
                .AddSingleton<IHotkeyListener, Win32HotkeyListener>()
                .AddSingleton<INativeHelper, Win32NativeHelper>()
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
                    if (configuration.Get<Settings>() is { } settings) return settings;
                    settings = new Settings();
                    configuration.Bind(settings);
                    return settings;
                })

                #endregion

                #region Avalonia Basic

                .AddSingleton<DialogManager>()
                .AddSingleton<ToastManager>()
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
                .AddSingleton<SettingsPageViewModel>()
                .AddSingleton<IMainViewPage, SettingsPage>()
                .AddSingleton<MainViewModel>()
                .AddSingleton<MainView>()

                #endregion

                #region Database

                .AddDbContext<IChatDatabase, ChatDbContext>(ServiceLifetime.Singleton)

                #endregion

                #region Initialize

                .AddSingleton<IAsyncInitializer, HotkeyInitializer>()
                .AddSingleton<IAsyncInitializer, SettingsInitializer>()
                .AddSingleton<IAsyncInitializer>(xx => xx.GetRequiredService<ChatContextManager>())
                .AddSingleton<IAsyncInitializer>(xx => xx.GetRequiredService<ChatDbContext>())

                #endregion

                #region Assistant Chat

                .AddSingleton<IKernelMixinFactory, KernelMixinFactory>()
                .AddTransient<IKernelMixin>(xx => xx.GetRequiredService<IKernelMixinFactory>().Create())
                .AddTransient<ITextGenerationService>(xx => xx.GetRequiredService<IKernelMixin>())
                .AddTransient<IChatCompletionService>(xx => xx.GetRequiredService<IKernelMixin>())
                .AddTransient<ITextGenerator>(xx => xx.GetRequiredService<IKernelMixin>())
                .AddTransient<ITextTokenizer>(xx => xx.GetRequiredService<IKernelMixin>())
                .AddSingleton<ChatContextManager>()
                .AddSingleton<IChatContextManager>(xx => xx.GetRequiredService<ChatContextManager>())
                .AddSingleton<IChatService, ChatService>()
                // .AddSingleton<IKernelMemory>(xx => new KernelMemoryBuilder()
                //     .Configure(builder =>
                //     {
                //         var baseFolder = Path.Combine(
                //             Path.GetDirectoryName(Environment.ProcessPath) ?? Environment.CurrentDirectory,
                //             "Assets",
                //             "text2vec-chinese-base");
                //         var generator = new Text2VecTextEmbeddingGenerator(
                //             Path.Combine(baseFolder, "tokenizer.json"),
                //             Path.Combine(baseFolder, "model.onnx"));
                //         builder.AddSingleton<ITextEmbeddingGenerator>(generator);
                //         builder.AddSingleton<ITextEmbeddingBatchGenerator>(generator);
                //         builder.AddIngestionEmbeddingGenerator(generator);
                //         builder.Services.AddSingleton<ITextGenerator>(_ => xx.GetRequiredService<ITextGenerator>());
                //         builder.AddSingleton(
                //             new TextPartitioningOptions
                //             {
                //                 MaxTokensPerParagraph = generator.MaxTokens,
                //                 OverlappingTokens = generator.MaxTokens / 20
                //             });
                //     })
                //     .Configure(builder => builder.Services.AddLogging(l => l.AddSimpleConsole()))
                //     .Build<MemoryServerless>())

            #endregion

        );

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private class ConsoleLogger<TCategory> : ILogger<TCategory>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            switch (logLevel)
            {
                case LogLevel.Error:
                case LogLevel.Critical:
                    Console.Error.WriteLine(formatter(state, exception));
                    break;
                default:
                    Console.WriteLine(formatter(state, exception));
                    break;
            }
        }
    }
}