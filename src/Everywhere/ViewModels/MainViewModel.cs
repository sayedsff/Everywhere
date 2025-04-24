using System.Collections.ObjectModel;
using Everywhere.Models;

namespace Everywhere.ViewModels;

public partial class MainViewModel : ReactiveViewModelBase
{
    public ObservableCollection<IVisualElement> RootElements { get; } = [];

    public MainViewModel(IUserInputTrigger userInputTrigger, IVisualElementContext visualElementContext)
    {
        userInputTrigger.ActionPanelRequested += () =>
        {
            RootElements.Clear();
            var element = visualElementContext.PointerOverElement;
            if (element != null)
                RootElements.Add(new OptimizedVisualElement(
                    element
                        .EnumerateAncestors()
                        .CurrentAndNext()
                        .Where(p => p.current.ProcessId != p.next.ProcessId)
                        .Select(p => p.current)
                        .First()));
        };
    }
}