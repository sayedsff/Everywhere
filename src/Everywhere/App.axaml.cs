using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Everywhere.Views;

namespace Everywhere;

public class App : Application
{
    private Window? mainWindow, debugWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        Task.WhenAll(ServiceLocator.Resolve<IEnumerable<IAsyncInitializer>>().Select(i => i.InitializeAsync()));
    }

    public override void OnFrameworkInitializationCompleted()
    {
        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime:
            {
                DisableAvaloniaDataAnnotationValidation();
                break;
            }
        }
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    private void HandleOpenMainWindowMenuItemClicked(object? sender, EventArgs e)
    {
        ShowWindow<MainView>(ref mainWindow);
    }

    private void HandleOpenDebugWindowMenuItemClicked(object? sender, EventArgs e)
    {
        ShowWindow<VisualTreeDebugger>(ref debugWindow);
    }

    private static void ShowWindow<TContent>(ref Window? window) where TContent : Control
    {
        if (window is { IsVisible: true })
        {
            var topmost = window.Topmost;
            window.Topmost = true;
            window.Activate();
            window.Topmost = topmost;
        }
        else
        {
            window?.Close();
            var content = ServiceLocator.Resolve<TContent>();
            content.To<ISetLogicalParent>().SetParent(null);
            window = new MainWindow
            {
                Content = content
            };
            window.Show();
        }
    }

    private void HandleExitMenuItemClicked(object? sender, EventArgs e)
    {
        Environment.Exit(0);
    }
}