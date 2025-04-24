using Avalonia;
using Everywhere.Interfaces;
using Everywhere.ViewModels;
using Everywhere.Views;
using Everywhere.Windows.Services;
using Microsoft.Extensions.DependencyInjection;
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

                .AddSingleton<MainViewModel>()
                .AddSingleton<MainView>()
                .AddSingleton<MainWindow>()

                .AddSingleton<IVisualElementContext, UIA3VisualElementContext>()
                .AddSingleton<IUserInputTrigger, Win32UserInputTrigger>()
            );

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}