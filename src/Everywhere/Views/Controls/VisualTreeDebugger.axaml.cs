using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace Everywhere.Views;

public partial class VisualTreeDebugger : UserControl
{
    public ObservableCollection<IVisualElement> RootElements { get; } = [];

    private readonly Window treeViewFocusMask;

    public VisualTreeDebugger(
        IUserInputTrigger userInputTrigger,
        IVisualElementContext visualElementContext,
        IWindowHelper windowHelper)
    {
        InitializeComponent();

        visualElementContext.KeyboardFocusedElementChanged += element =>
        {
            Debug.WriteLine(element?.ToString());
        };

        userInputTrigger.KeyboardHotkeyActivated += () =>
        {
            RootElements.Clear();
            var element = visualElementContext.PointerOverElement;
            if (element == null) return;
            element = element
                .GetAncestors()
                .CurrentAndNext()
                .Where(p => p.current.ProcessId != p.next.ProcessId)
                .Select(p => p.current)
                .First();
            RootElements.Add(element);
        };

        treeViewFocusMask = new Window
        {
            Topmost = true,
            IsHitTestVisible = false,
            ShowInTaskbar = false,
            ShowActivated = false,
            SystemDecorations = SystemDecorations.None,
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
            Background = null,
            Content = new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.DodgerBlue,
                Opacity = 0.5
            }
        };
        windowHelper.SetWindowHitTestInvisible(treeViewFocusMask);

        var keyboardFocusMask = new Window
        {
            Topmost = true,
            IsHitTestVisible = false,
            ShowInTaskbar = false,
            ShowActivated = false,
            SystemDecorations = SystemDecorations.None,
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
            Background = null,
            Content = new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Crimson,
                Opacity = 0.5
            }
        };
        windowHelper.SetWindowHitTestInvisible(keyboardFocusMask);

        visualElementContext.KeyboardFocusedElementChanged += element =>
        {
            Dispatcher.UIThread.Invoke(() => SetMask(keyboardFocusMask, element));
        };
    }

    private void HandleTreeViewPointerMoved(object? sender, PointerEventArgs e)
    {
        var element = e.Source as StyledElement;
        while (element != null)
        {
            element = element.Parent;
            if (element is TreeViewItem { DataContext: IVisualElement visualElement })
            {
                SetMask(treeViewFocusMask, visualElement);
                return;
            }
        }

        SetMask(treeViewFocusMask, null);
    }

    private static void SetMask(Window mask, IVisualElement? element)
    {
        if (element == null)
        {
            mask.Hide();
        }
        else
        {
            mask.Show();
            var boundingRectangle = element.BoundingRectangle;
            mask.Position = new PixelPoint(boundingRectangle.X, boundingRectangle.Y);
            mask.Width = boundingRectangle.Width / mask.DesktopScaling;
            mask.Height = boundingRectangle.Height / mask.DesktopScaling;
        }
    }
}