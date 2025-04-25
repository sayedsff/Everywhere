using Avalonia;
using Everywhere.Extensions;
using Everywhere.Interfaces;
using Everywhere.ViewModels;
using Everywhere.Views;
using Everywhere.Windows.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace Everywhere.Windows;

public static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        ServiceLocator.Build(
            x => x
                .AddSingleton<ISukiDialogManager, SukiDialogManager>()
                .AddSingleton<ISukiToastManager, SukiToastManager>()

                .AddSingleton<VisualTreeDebuggerWindowViewModel>()
                .AddSingleton<VisualTreeDebuggerWindow>()
                .AddTransient<PointerActionWindowViewModel>()
                .AddTransient<PointerActionWindow>()

                .AddSingleton<IVisualElementContext, UIA3VisualElementContext>()
                .AddSingleton<IUserInputTrigger, Win32UserInputTrigger>()
                .AddOpenAIChatCompletion(
                    modelId: "gpt-4o",
                    apiKey: Environment.GetEnvironmentVariable("NODIS_API_KEY", EnvironmentVariableTarget.User).NotNull("NODIS_API_KEY is not set")
                )
            );

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}