using System.Collections.ObjectModel;
using Avalonia.Threading;
using Everywhere.Views;

namespace Everywhere.ViewModels;

public partial class VisualTreeDebuggerWindowViewModel : ReactiveViewModelBase
{
    public ObservableCollection<IVisualElement> RootElements { get; } = [];

    private CancellationTokenSource? cancellationTokenSource;

    public VisualTreeDebuggerWindowViewModel(
        IUserInputTrigger userInputTrigger,
        IVisualElementContext visualElementContext,
        AgentFloatingWindow agentFloatingWindow)
    {
        visualElementContext.KeyboardFocusedElementChanged += element =>
        {
            Console.WriteLine(element?.ToString());

            cancellationTokenSource?.Cancel();
            cancellationTokenSource = new CancellationTokenSource();
            agentFloatingWindow.ViewModel.SetTargetElementAsync(element, cancellationTokenSource.Token).Detach();
        };

        userInputTrigger.PointerHotkeyActivated += point =>
        {
            // RootElements.Clear();
            // var element = visualElementContext.TargetElement;
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
                    var window = ServiceLocator.Resolve<AgentFloatingWindow>();
                    window.Position = point;
                    window.IsVisible = true;
                });
        };
    }
}