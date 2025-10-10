using Avalonia.Threading;
using Everywhere.Common;
using Everywhere.Configuration;
using Everywhere.Interop;
using Everywhere.Views;
using Microsoft.Extensions.Logging;

namespace Everywhere.Initialization;

public class HotkeyInitializer(
    Settings settings,
    IHotkeyListener hotkeyListener,
    IVisualElementContext visualElementContext,
    ILogger<HotkeyInitializer> logger
) : IAsyncInitializer
{
    public AsyncInitializerPriority Priority => AsyncInitializerPriority.AfterSettings;

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
        hotkeyListener.KeyboardHotkey = settings.Behavior.ChatHotkey;
        hotkeyListener.KeyboardHotkeyActivated += () =>
        {
            ThreadPool.QueueUserWorkItem(_ =>
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
            });
        };
        // hotkeyListener.PointerHotkeyActivated += point =>
        // {
        //     Dispatcher.UIThread.InvokeOnDemandAsync(
        //         () =>
        //         {
        //             var window = ServiceLocator.Resolve<ChatFloatingWindow>();
        //             window.Position = point;
        //             window.IsOpened = true;
        //         });
        // };

        return Task.CompletedTask;
    }
}