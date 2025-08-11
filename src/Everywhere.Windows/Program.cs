using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Everywhere.Chat;
using Everywhere.Database;
using Everywhere.Extensions;
using Everywhere.Initialization;
using Everywhere.Interfaces;
using Everywhere.Models;
using Everywhere.ViewModels;
using Everywhere.Views;
using Everywhere.Views.Pages;
using Everywhere.Windows.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using ShadUI;

namespace Everywhere.Windows;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Entrance.Initialize(args);

        ServiceLocator.Build(x => x

                #region Basic

                .AddLogging(builder => builder.AddSerilog(dispose: true))
                .AddSingleton<IRuntimeConstantProvider, RuntimeConstantProvider>()
                .AddSingleton<IVisualElementContext, Win32VisualElementContext>()
                .AddSingleton<IHotkeyListener, Win32HotkeyListener>()
                .AddSingleton<INativeHelper, Win32NativeHelper>()
                .AddSettings()

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
                .AddTransient<IMainViewPageFactory, SettingsCategoryPageFactory>()
                .AddSingleton<AboutPageViewModel>()
                .AddSingleton<IMainViewPage, AboutPage>()
                .AddSingleton<WelcomeViewModel>(xx =>
                {
                    xx.GetRequiredService<DialogManager>().Register<WelcomeView, WelcomeViewModel>();
                    return new WelcomeViewModel();
                })
                .AddSingleton<MainViewModel>()
                .AddSingleton<MainView>()

                #endregion

                #region Database

                .AddChatDbContextAndStorage()

                #endregion

                #region Initialize

                .AddTransient<IAsyncInitializer, HotkeyInitializer>()
                .AddTransient<IAsyncInitializer, SettingsInitializer>()
                .AddTransient<IAsyncInitializer>(xx => xx.GetRequiredService<ChatContextManager>())

                #endregion

                #region Assistant Chat

                .AddSingleton<IKernelMixinFactory, KernelMixinFactory>()
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
}