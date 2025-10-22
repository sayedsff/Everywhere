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
/// <param name="hotkeyListener"></param>
/// <param name="visualElementContext"></param>
/// <param name="logger"></param>
public class ChatWindowInitializer(
    Settings settings,
    IHotkeyListener hotkeyListener,
    IVisualElementContext visualElementContext,
    ILogger<ChatWindowInitializer> logger
) : IAsyncInitializer
{
    public AsyncInitializerPriority Priority => AsyncInitializerPriority.Startup;

    private readonly Lock _syncLock = new();

    private IDisposable? _chatHotkeySubscription;

    public Task InitializeAsync()
    {
        // initialize hotkey listener
        settings.ChatWindow.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(settings.ChatWindow.Hotkey))
            {
                HandleChatHotkeyChanged(settings.ChatWindow.Hotkey);
            }
        };

        HandleChatHotkeyChanged(settings.ChatWindow.Hotkey);

        Dispatcher.UIThread.Invoke(() =>
        {
            // Preload ChatWindow to avoid delay on first open
            var chatWindow = ServiceLocator.Resolve<ChatWindow>();
            chatWindow.ViewModel.IsOpened = true;
            chatWindow.ViewModel.IsOpened = false;
        });

        return Task.CompletedTask;
    }

    private void HandleChatHotkeyChanged(KeyboardHotkey hotkey)
    {
        using var _ = _syncLock.EnterScope();

        _chatHotkeySubscription?.Dispose();
        if (!hotkey.IsValid) return;

        _chatHotkeySubscription = hotkeyListener.Register(
            hotkey,
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
                    if (hWnd == chatWindow.TryGetPlatformHandle()?.Handle) return;

                    chatWindow.ViewModel.TryFloatToTargetElementAsync(element).Detach(logger.ToExceptionHandler());
                });
            }));
    }
}