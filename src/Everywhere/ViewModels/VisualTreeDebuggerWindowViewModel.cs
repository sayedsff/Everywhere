using System.Collections.ObjectModel;
using System.Diagnostics;
using Everywhere.Models;

namespace Everywhere.ViewModels;

public partial class VisualTreeDebuggerWindowViewModel : ReactiveViewModelBase
{
    public ObservableCollection<IVisualElement> RootElements { get; } = [];

    public VisualTreeDebuggerWindowViewModel(
        IUserInputTrigger userInputTrigger,
        IVisualElementContext visualElementContext)
    {
        visualElementContext.KeyboardFocusedElementChanged += element =>
        {
            Debug.WriteLine(element?.ToString());
        };

        userInputTrigger.PointerHotkeyActivated += _ =>
        {
            RootElements.Clear();
            var element = visualElementContext.PointerOverElement;
            if (element != null)
                RootElements.Add(new OptimizedVisualElement(
                    element
                        .GetAncestors()
                        .CurrentAndNext()
                        .Where(p => p.current.ProcessId != p.next.ProcessId)
                        .Select(p => p.current)
                        .First()));
        };
    }
}