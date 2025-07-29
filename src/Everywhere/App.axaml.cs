using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Everywhere.Utils;
using Everywhere.Views;
using Window = Avalonia.Controls.Window;
#if DEBUG
using Everywhere.Enums;
using Everywhere.Models;
using Everywhere.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using ShadUI;
#endif

namespace Everywhere;

public class App : Application
{
    public TopLevel TopLevel { get; } = new Window();

    private TransientWindow? mainWindow, debugWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

#if DEBUG
        if (Design.IsDesignMode)
        {
            ServiceLocator.Build(x => x

                    #region Basic

                    .AddSingleton<IRuntimeConstantProvider, DesignTimeRuntimeConstantProvider>()
                    .AddSingleton<IVisualElementContext, DesignTimeVisualElementContext>()
                    .AddSingleton<IHotkeyListener, DesignTimeHotkeyListener>()
                    .AddSingleton<INativeHelper, DesignTimeNativeHelper>()
                    .AddSingleton<Settings>()

                    #endregion

                    #region Avalonia Basic

                    .AddSingleton<DialogManager>()
                    .AddSingleton<ToastManager>()

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

            );
        }
#endif

        try
        {
            foreach (var group in ServiceLocator.Resolve<IEnumerable<IAsyncInitializer>>().GroupBy(i => i.Priority).OrderByDescending(g => g.Key))
            {
                Task.WhenAll(group.Select(i => i.InitializeAsync())).WaitOnDispatcherFrame();
            }
        }
        catch (Exception ex)
        {
            NativeMessageBox.Show(
                "Initialization Error",
                $"An error occurred during application initialization:\n{ex.Message}\n\nPlease check the logs for more details.",
                NativeMessageBoxButtons.Ok,
                NativeMessageBoxIcon.Error);
        }
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

    /// <summary>
    /// Flag to prevent multiple calls to ShowWindow method from event loop.
    /// </summary>
    private static bool isShowWindowBusy;

    private static void ShowWindow<TContent>(ref TransientWindow? window) where TContent : Control
    {
        if (isShowWindowBusy) return;
        try
        {
            isShowWindowBusy = true;
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
                window = new TransientWindow
                {
                    Content = content
                };
                window.Show();
            }
        }
        finally
        {
            isShowWindowBusy = false;
        }
    }

    private void HandleExitMenuItemClicked(object? sender, EventArgs e)
    {
        Environment.Exit(0);
    }
}

#if DEBUG

file class DesignTimeRuntimeConstantProvider : IRuntimeConstantProvider
{
    public object? this[RuntimeConstantType type] => null;
}

file class DesignTimeVisualElementContext : IVisualElementContext
{
    public event IVisualElementContext.KeyboardFocusedElementChangedHandler? KeyboardFocusedElementChanged;
    public IVisualElement? KeyboardFocusedElement => null;
    public IVisualElement? PointerOverElement => null;
    public IVisualElement? ElementFromPoint(PixelPoint point) => null;
    public Task<IVisualElement?> PickElementAsync(PickElementMode mode) => Task.FromResult<IVisualElement?>(null);
}

file class DesignTimeHotkeyListener : IHotkeyListener
{
    public event PointerHotkeyActivatedHandler? PointerHotkeyActivated;
    public event KeyboardHotkeyActivatedHandler? KeyboardHotkeyActivated;
    public KeyboardHotkey KeyboardHotkey { get; set; }
    public IKeyboardHotkeyScope StartCaptureKeyboardHotkey() => throw new NotSupportedException();
}

file class DesignTimeNativeHelper : INativeHelper
{
    public void SetWindowNoFocus(Window window) { }
    public void SetWindowAutoHide(Window window) { }
    public void SetWindowHitTestInvisible(Window window) { }
    public void SetWindowCornerRadius(Window window, CornerRadius cornerRadius) { }
    public void HideWindowWithoutAnimation(Window window) { }
    public Task<WriteableBitmap?> GetClipboardBitmapAsync() => Task.FromResult<WriteableBitmap?>(null);
}

#endif