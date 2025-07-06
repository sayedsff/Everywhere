using System.Globalization;
using Avalonia.Threading;
using Everywhere.I18N;
using Everywhere.Models;
using Everywhere.Views;

namespace Everywhere.Initialization;

public class AssistantInitializer(
    Settings settings,
    IHotkeyListener hotkeyListener,
    IVisualElementContext visualElementContext) : IAsyncInitializer
{
    public Task InitializeAsync()
    {
        LocaleManager.CurrentLocale = CultureInfo.CurrentUICulture.Name;

        visualElementContext.KeyboardFocusedElementChanged += element =>
        {
            if (!settings.Behavior.ShowAssistantFloatingWindowWhenInput) return;
            if (element is not { Type: VisualElementType.TextEdit } ||
                (element.States & (
                    VisualElementStates.Offscreen |
                    VisualElementStates.Disabled |
                    VisualElementStates.ReadOnly |
                    VisualElementStates.Password)) != 0)
            {
                element = null;
            }

            Dispatcher.UIThread.InvokeOnDemandAsync(
                () => ServiceLocator.Resolve<AssistantFloatingWindow>().ViewModel.TryFloatToTargetElementAsync(element).Detach()).Detach();
        };

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
            var element = visualElementContext.KeyboardFocusedElement?.GetAncestors(true)
                .CurrentAndNext().FirstOrDefault(t => t.Current.ProcessId != t.Next.ProcessId).Current ??
                visualElementContext.PointerOverElement?.GetAncestors(true).LastOrDefault();
            if (element == null) return;
            Dispatcher.UIThread.InvokeOnDemandAsync(
                () => ServiceLocator.Resolve<AssistantFloatingWindow>().ViewModel.TryFloatToTargetElementAsync(element, true).Detach()).Detach();
        };
        // hotkeyListener.PointerHotkeyActivated += point =>
        // {
        //     Dispatcher.UIThread.InvokeOnDemandAsync(
        //         () =>
        //         {
        //             var window = ServiceLocator.Resolve<AssistantFloatingWindow>();
        //             window.Position = point;
        //             window.IsVisible = true;
        //         });
        // };

        return Task.CompletedTask;
    }
}