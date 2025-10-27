using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Everywhere.Common;
using Everywhere.Configuration;
using Everywhere.Interop;
using Everywhere.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using ShadUI;
using Window = Avalonia.Controls.Window;

namespace Everywhere;

public class App : Application
{
    public TopLevel TopLevel { get; } = new Window();

    private TransientWindow? _mainWindow, _debugWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Log.Logger.Error(e.Exception, "UI Thread Unhandled Exception");

            NativeMessageBox.Show(
                "Unexpected Error",
                $"An unexpected error occurred:\n{e.Exception.Message}\n\nPlease check the logs for more details.",
                NativeMessageBoxButtons.Ok,
                NativeMessageBoxIcon.Error);

            e.Handled = true;
        };

#if DEBUG
        if (Design.IsDesignMode)
        {
            ServiceLocator.Build(x => x

                    #region Basic

                    .AddSingleton<IRuntimeConstantProvider, DesignTimeRuntimeConstantProvider>()
                    .AddSingleton<IVisualElementContext, DesignTimeVisualElementContext>()
                    .AddSingleton<IShortcutListener, DesignTimeShortcutListener>()
                    .AddSingleton<INativeHelper, DesignTimeNativeHelper>()
                    .AddSingleton<Settings>()

                    #endregion

                    #region Avalonia Basic

                    .AddSingleton<DialogManager>()
                    .AddSingleton<ToastManager>()

                    #endregion

                    #region View & ViewModel

                    .AddSingleton<VisualTreeDebugger>()
                    .AddSingleton<ChatWindowViewModel>()
                    .AddSingleton<ChatWindow>()
                    .AddSingleton<MainViewModel>()
                    .AddSingleton<MainView>()

                #endregion

            );
        }
#endif

        try
        {
            foreach (var group in ServiceLocator
                         .Resolve<IEnumerable<IAsyncInitializer>>()
                         .GroupBy(i => i.Priority)
                         .OrderBy(g => g.Key))
            {
                Task.WhenAll(group.Select(i => i.InitializeAsync())).WaitOnDispatcherFrame();
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Fatal(ex, "Failed to initialize application");

            NativeMessageBox.Show(
                "Initialization Error",
                $"An error occurred during application initialization:\n{ex.Message}\n\nPlease check the logs for more details.",
                NativeMessageBoxButtons.Ok,
                NativeMessageBoxIcon.Error);
        }

        Log.ForContext<App>().Information("Application started");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime:
            {
                DisableAvaloniaDataAnnotationValidation();
                ShowMainWindowOnNeeded();
                break;
            }
        }
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToList();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    /// <summary>
    /// Show the main window if it was not shown before or the version has changed.
    /// </summary>
    private void ShowMainWindowOnNeeded()
    {
        // If the --ui command line argument is present, show the main window.
        if (Environment.GetCommandLineArgs().Contains("--ui"))
        {
            ShowWindow<MainView>(ref _mainWindow);
            return;
        }

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
        var settings = ServiceLocator.Resolve<Settings>();
        if (settings.Internal.PreviousLaunchVersion == version) return;

        ShowWindow<MainView>(ref _mainWindow);
    }

    private void HandleOpenMainWindowMenuItemClicked(object? sender, EventArgs e)
    {
        ShowWindow<MainView>(ref _mainWindow);
    }

    private void HandleOpenDebugWindowMenuItemClicked(object? sender, EventArgs e)
    {
        ShowWindow<VisualTreeDebugger>(ref _debugWindow);
    }

    /// <summary>
    /// Flag to prevent multiple calls to ShowWindow method from event loop.
    /// </summary>
    private static bool _isShowWindowBusy;

    private static void ShowWindow<TContent>(ref TransientWindow? window) where TContent : Control
    {
        if (_isShowWindowBusy) return;
        try
        {
            _isShowWindowBusy = true;
            if (window is { IsVisible: true })
            {
                var topmost = window.Topmost;
                window.Topmost = false;
                window.Activate();
                window.Topmost = true;
                window.Topmost = topmost;
            }
            else
            {
                window?.Close();
                var content = ServiceLocator.Resolve<TContent>();
                content.To<ISetLogicalParent>().SetParent(null);
                window = new TransientWindow
                {
                    Content = content
                };
                window.Show();
            }
        }
        finally
        {
            _isShowWindowBusy = false;
        }
    }

    private void HandleExitMenuItemClicked(object? sender, EventArgs e)
    {
        Environment.Exit(0);
    }
}

#if DEBUG
#pragma warning disable CS0067 // The event is for design-time only.
file class DesignTimeRuntimeConstantProvider : IRuntimeConstantProvider
{
    public object? this[RuntimeConstantType type] => null;
}

file class DesignTimeVisualElementContext : IVisualElementContext
{
    public event IVisualElementContext.KeyboardFocusedElementChangedHandler? KeyboardFocusedElementChanged;
    public IVisualElement? KeyboardFocusedElement => null;
    public IVisualElement? ElementFromPoint(PixelPoint point, PickElementMode mode = PickElementMode.Element) => null;
    public IVisualElement? ElementFromPointer(PickElementMode mode = PickElementMode.Element) => null;
    public Task<IVisualElement?> PickElementAsync(PickElementMode mode) => Task.FromResult<IVisualElement?>(null);
}

file class DesignTimeShortcutListener : IShortcutListener
{
    public IDisposable Register(KeyboardShortcut shortcut, Action handler) => throw new NotSupportedException();
    public IDisposable Register(MouseShortcut shortcut, Action handler) => throw new NotSupportedException();
    public IKeyboardShortcutScope StartCaptureKeyboardShortcut() => throw new NotSupportedException();
}

file class DesignTimeNativeHelper : INativeHelper
{
    public bool IsInstalled => false;
    public bool IsAdministrator => false;
    public bool IsUserStartupEnabled { get; set; }
    public bool IsAdministratorStartupEnabled { get; set; }
    public void RestartAsAdministrator() { }
    public Task<WriteableBitmap?> GetClipboardBitmapAsync() => Task.FromResult<WriteableBitmap?>(null);
    public void ShowDesktopNotification(string message, string? title) { }
    public void OpenFileLocation(string fullPath) { }
}

#pragma warning restore CS0067 // The event is for design-time only.
#endif