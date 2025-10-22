using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Everywhere.Chat.Plugins;
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

        var diff = new TextDifference("C:\\Linux\\a.txt");
        var origin =
            """
            using OpenAI;
            using OpenAI.Chat;
            using BinaryContent = System.ClientModel.BinaryContent;
            using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
            using TextContent = Microsoft.Extensions.AI.TextContent;

            namespace Everywhere.AI;
            public sealed class OpenAIKernelMixin : KernelMixinBase
                {
                    ChatCompletionService = new OptimizedOpenAIApiClient(
                        new OptimizedChatClient(
                            customAssistant.ModelId,
                            // some models don't need API key (e.g. LM Studio)
                            new ApiKeyCredential(customAssistant.ApiKey.IsNullOrWhiteSpace() ? "NO_API_KEY" : customAssistant.ApiKey),
                            new OpenAIClientOptions
                            {
                                Endpoint = new Uri(customAssistant.Endpoint, UriKind.Absolute)
                            }
                        ).AsIChatClient(),
                        this
                                update.AdditionalProperties = ApplyReasoningProperties(update.AdditionalProperties);
                            }
                            yield return update;
                        }
                    }
            """;
        var modified =
            """
            using OpenAI.Chat;
            using BinaryContent = System.ClientModel.BinaryContent;
            using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
            using FunctionCallContent = Microsoft.Extensions.AI.FunctionCallContent;
            using TextContent = Microsoft.Extensions.AI.TextContent;

            namespace Everywhere.AI;
            public sealed class OpenAIKernelMixin : KernelMixinBase
                {
                    ChatCompletionService = new OptimizedOpenAIApiClient(
                        new OptimizedChatClient(
                            ModelId,
                            // some models don't need API key (e.g. LM Studio)
                            new ApiKeyCredential(ApiKey.IsNullOrWhiteSpace() ? "NO_API_KEY" : ApiKey),
                            new OpenAIClientOptions
                            {
                                Endpoint = new Uri(Endpoint, UriKind.Absolute)
                            }
                        ).AsIChatClient(),
                        this
            public sealed class OpenAIKernelMixin : KernelMixinBase
                                update.AdditionalProperties = ApplyReasoningProperties(update.AdditionalProperties);
                            }

                            // Ensure that all FunctionCallContent items have a unique CallId.
                            for (var i = 0; i < update.Contents.Count; i++)
                            {
                                var item = update.Contents[i];
                                if (item is FunctionCallContent { Name.Length: > 0, CallId: null or { Length: 0 } } missingIdContent)
                                {
                                    // Generate a unique ToolCallId for the function call update.
                                    update.Contents[i] = new FunctionCallContent(
                                        Guid.CreateVersion7().ToString("N"),
                                        missingIdContent.Name,
                                        missingIdContent.Arguments);
                                }
                            }

                            yield return update;
                        }
                    }
            """;

        TextDifferenceBuilder.BuildLineDiff(diff, origin, modified);

        // diff.AcceptAll();
        var summary = diff.ToUnifiedDiff(origin, default);
        var equals = Equals(diff.Apply(origin), modified);

        new TransientWindow
        {
            Content = new StackPanel
            {
                Children =
                {
                    new TextDifferenceSummaryView
                    {
                        TextDifference = diff,
                        OriginalText = origin
                    },
                    new TextDifferenceEditor
                    {
                        TextDifference = diff,
                        OriginalText = origin,
                        OnlyAccepted = false,
                        ShowLineNumbers = true
                    }
                }
            }
        }.Show();

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

        Log.Logger.Information("Application started");
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
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

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

file class DesignTimeHotkeyListener : IHotkeyListener
{
    public IDisposable Register(KeyboardHotkey hotkey, Action handler) => throw new NotSupportedException();
    public IDisposable Register(MouseHotkey hotkey, Action handler) => throw new NotSupportedException();
    public IKeyboardHotkeyScope StartCaptureKeyboardHotkey() => throw new NotSupportedException();
}

file class DesignTimeNativeHelper : INativeHelper
{
    public bool IsInstalled => false;
    public bool IsAdministrator => false;
    public bool IsUserStartupEnabled { get; set; }
    public bool IsAdministratorStartupEnabled { get; set; }
    public void RestartAsAdministrator() { }
    public void SetWindowNoFocus(Window window) { }
    public void SetWindowHitTestInvisible(Window window) { }
    public void HideWindowWithoutAnimation(Window window) { }
    public Task<WriteableBitmap?> GetClipboardBitmapAsync() => Task.FromResult<WriteableBitmap?>(null);
    public void ShowDesktopNotification(string message, string? title) { }
}

#pragma warning restore CS0067 // The event is for design-time only.
#endif