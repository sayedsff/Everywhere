using Avalonia.Threading;
using Everywhere.Models;
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
    public int Priority => 0;

    public Task InitializeAsync()
    {
        // initialize hotkey listener
        settings.Behavior.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(settings.Behavior.AssistantHotkey))
            {
                hotkeyListener.KeyboardHotkey = settings.Behavior.AssistantHotkey;
            }
        };
        hotkeyListener.KeyboardHotkey = settings.Behavior.AssistantHotkey;
        hotkeyListener.KeyboardHotkeyActivated += () =>
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var element = visualElementContext.KeyboardFocusedElement?
                        .GetAncestors(true)
                        .CurrentAndNext()
                        .FirstOrDefault(t => t.Current.Type == VisualElementType.TextEdit || t.Current.ProcessId != t.Next.ProcessId).Current ??
                    visualElementContext.PointerOverElement?
                        .GetAncestors(true)
                        .LastOrDefault();
                if (element == null) return;
                Dispatcher.UIThread.Invoke(() =>
                    ServiceLocator.Resolve<ChatFloatingWindow>().ViewModel.TryFloatToTargetElementAsync(element).Detach(logger.ToExceptionHandler()));
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