using Avalonia.Threading;
using Everywhere.Models;
using Everywhere.Views;

namespace Everywhere.Initialization;

public class AssistantInitializer(
    Settings settings,
    IUserInputTrigger userInputTrigger,
    IVisualElementContext visualElementContext,
    AssistantFloatingWindow assistantFloatingWindow) : IAsyncInitializer
{
    public Task InitializeAsync()
    {
        visualElementContext.KeyboardFocusedElementChanged += element =>
        {
            if (settings.Behavior is not
                {
                    IsInputAssistantEnabled: true,
                    ShowInputAssistantFloatingWindow: true
                }) return;
            assistantFloatingWindow.ViewModel.TryFloatToTargetElementAsync(element).Detach();
        };

        userInputTrigger.PointerHotkeyActivated += point =>
        {
            Dispatcher.UIThread.InvokeOnDemandAsync(
                () =>
                {
                    var window = ServiceLocator.Resolve<AssistantFloatingWindow>();
                    window.Position = point;
                    window.IsVisible = true;
                });
        };

        return Task.CompletedTask;
    }
}