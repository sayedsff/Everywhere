using Avalonia.Threading;
using Everywhere.Common;
using Everywhere.Configuration;
using Everywhere.Interop;
using Everywhere.Views;
using Microsoft.Extensions.Logging;

namespace Everywhere.Initialization;

/// <summary>
/// Initializes the chat window hotkey listener and preloads the chat window.
/// </summary>
/// <param name="settings"></param>
/// <param name="shortcutListener"></param>
/// <param name="visualElementContext"></param>
/// <param name="logger"></param>
public class ChatWindowInitializer(
    Settings settings,
    IShortcutListener shortcutListener,
    IVisualElementContext visualElementContext,
    ILogger<ChatWindowInitializer> logger
) : IAsyncInitializer
{
    public AsyncInitializerPriority Priority => AsyncInitializerPriority.Startup;

    private readonly Lock _syncLock = new();

    private IDisposable? _chatShortcutSubscription;

    public Task InitializeAsync()
    {
        // initialize hotkey listener
        settings.ChatWindow.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(settings.ChatWindow.Shortcut))
            {
                HandleChatShortcutChanged(settings.ChatWindow.Shortcut);
            }
        };

        HandleChatShortcutChanged(settings.ChatWindow.Shortcut);

        // Preload ChatWindow to avoid delay on first open
        Dispatcher.UIThread.Invoke(() => ServiceLocator.Resolve<ChatWindow>().Initialize());

        return Task.CompletedTask;
    }

    private void HandleChatShortcutChanged(KeyboardShortcut shortcut)
    {
        using var _ = _syncLock.EnterScope();

        _chatShortcutSubscription?.Dispose();
        if (!shortcut.IsValid) return;

        _chatShortcutSubscription = shortcutListener.Register(
            shortcut,
            () => ThreadPool.QueueUserWorkItem(_ =>
            {
                var element = visualElementContext.KeyboardFocusedElement ??
                    visualElementContext.ElementFromPointer()?
                        .GetAncestors(true)
                        .LastOrDefault();
                if (element == null) return;

                var hWnd = element.NativeWindowHandle;
                Dispatcher.UIThread.Invoke(() =>
                {
                    var chatWindow = ServiceLocator.Resolve<ChatWindow>();
                    if (hWnd == chatWindow.TryGetPlatformHandle()?.Handle)
                    {
                        chatWindow.ViewModel.IsOpened = false; // Hide chat window if it's already focused
                    }
                    else
                    {
                        chatWindow.ViewModel.TryFloatToTargetElementAsync(element).Detach(logger.ToExceptionHandler());
                    }
                });
            }));
    }
}