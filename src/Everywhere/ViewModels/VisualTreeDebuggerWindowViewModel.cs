using System.Collections.ObjectModel;
using Avalonia.Threading;
using Everywhere.Views;

namespace Everywhere.ViewModels;

public partial class VisualTreeDebuggerWindowViewModel : ReactiveViewModelBase
{
    public ObservableCollection<IVisualElement> RootElements { get; } = [];

    public VisualTreeDebuggerWindowViewModel(IUserInputTrigger userInputTrigger, IVisualElementContext visualElementContext)
    {
        userInputTrigger.PointerHotkeyActivated += point =>
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

            Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    var window = ServiceLocator.Resolve<PointerActionWindow>();
                    window.Position = point;
                    window.Show();
                });
        };
    }
}