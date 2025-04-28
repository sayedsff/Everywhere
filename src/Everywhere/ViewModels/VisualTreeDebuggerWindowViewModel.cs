using System.Collections.ObjectModel;
using Everywhere.Models;
using Everywhere.Views;

namespace Everywhere.ViewModels;

public partial class VisualTreeDebuggerWindowViewModel : ReactiveViewModelBase
{
    public ObservableCollection<IVisualElement> RootElements { get; } = [];

    public VisualTreeDebuggerWindowViewModel(IUserInputTrigger userInputTrigger, IVisualElementContext visualElementContext)
    {
        userInputTrigger.KeyboardActionTriggered += () =>
        {
            // RootElements.Clear();
            // var element = visualElementContext.PointerOverElement;
            // if (element != null)
            //     RootElements.Add(new OptimizedVisualElement(
            //         element
            //             .GetAncestors()
            //             .CurrentAndNext()
            //             .Where(p => p.current.ProcessId != p.next.ProcessId)
            //             .Select(p => p.current)
            //             .First()));

            ServiceLocator.Resolve<PointerActionWindow>().Show();
        };
    }
}