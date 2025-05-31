using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Models;

namespace Everywhere.Views;

public partial class VisualTreeDebugger : UserControl
{
    private readonly IVisualElementContext visualElementContext;
    private readonly ObservableCollection<IVisualElement> rootElements = [];
    private readonly IReadOnlyList<VisualElementProperty> properties = typeof(DebuggerVisualElement)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Select(p => new VisualElementProperty(p))
        .ToReadOnlyList();
    private readonly OverlayWindow treeViewPointerOverOverlayWindow;

#if DEBUG
    [Obsolete("This constructor is for design time only.", true)]
    public VisualTreeDebugger()
    {
        visualElementContext = ServiceLocator.Resolve<IVisualElementContext>();
        treeViewPointerOverOverlayWindow = new OverlayWindow();
    }
#endif

    public VisualTreeDebugger(
        IHotkeyListener hotkeyListener,
        IVisualElementContext visualElementContext)
    {
        this.visualElementContext = visualElementContext;

        InitializeComponent();

        VisualTreeView.ItemsSource = rootElements;
        PropertyItemsControl.ItemsSource = properties;

        hotkeyListener.KeyboardHotkeyActivated += () =>
        {
            rootElements.Clear();
            var element = visualElementContext.PointerOverElement;
            if (element == null) return;
            element = element
                .GetAncestors()
                .LastOrDefault() ?? element;
            rootElements.Add(element);
        };

        treeViewPointerOverOverlayWindow = new OverlayWindow
        {
            Content = new Border
            {
                Background = Brushes.DodgerBlue,
                Opacity = 0.2
            },
        };

        var keyboardFocusMask = new OverlayWindow
        {
            Content = new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Crimson,
                Opacity = 0.5
            }
        };

        visualElementContext.KeyboardFocusedElementChanged += element =>
        {
            Dispatcher.UIThread.InvokeOnDemand(() =>
            {
                if (ShowKeyboardFocusedElementCheckBox.IsChecked is true) keyboardFocusMask.UpdateForVisualElement(element);
            });
        };

        ShowKeyboardFocusedElementCheckBox.IsCheckedChanged += delegate
        {
            if (ShowKeyboardFocusedElementCheckBox.IsChecked is not true) keyboardFocusMask.UpdateForVisualElement(null);
        };
    }

    private void HandleVisualTreeViewPointerMoved(object? sender, PointerEventArgs e)
    {
        IVisualElement? visualElement = null;
        var element = e.Source as StyledElement;
        while (element != null)
        {
            element = element.Parent;
            if (element != null && (visualElement = element.DataContext as IVisualElement) != null)
            {
                break;
            }
        }

        treeViewPointerOverOverlayWindow.UpdateForVisualElement(visualElement);
    }

    private void HandleVisualTreeViewSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var debuggerElement = VisualTreeView.SelectedItem is not IVisualElement selectedItem ? null : new DebuggerVisualElement(selectedItem);
        foreach (var property in properties)
        {
            property.Target = debuggerElement;
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.Title = nameof(VisualTreeDebugger);
        }
    }

    private async void HandlePickElementButtonClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            rootElements.Clear();
            if (await visualElementContext.PickElementAsync(PickElementMode.Element) is { } element)
            {
                rootElements.Add(element);
            }
        }
        catch
        {
            // ignored
        }
    }

    private async void HandleCaptureButtonClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (VisualTreeView.SelectedItem is not IVisualElement selectedItem) return;
            CaptureImage.Source = await selectedItem.CaptureAsync();
        }
        catch (Exception ex)
        {
            CaptureImage.Source = null;
            Debug.WriteLine(ex);
        }
    }
}

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
internal class DebuggerVisualElement(IVisualElement element) : ObservableObject
{
    public string? Name => element.Name;

    public VisualElementType Type => element.Type;

    public VisualElementStates States => element.States;
    
    public IVisualElement? Parent => element.Parent;

    public IVisualElement? PreviousSibling => element.PreviousSibling;

    public IVisualElement? NextSibling => element.NextSibling;

    public int ProcessId => element.ProcessId;

    public string ProcessName
    {
        get
        {
            try
            {
                using var process = Process.GetProcessById(ProcessId);
                return process.ProcessName;
            }
            catch
            {
                return "Unknown";
            }
        }
    }

    public nint NativeWindowHandle => element.NativeWindowHandle;

    public PixelRect BoundingRectangle => element.BoundingRectangle;

    public string? Text
    {
        get => element.GetText();
        set
        {
            if (value == null) return;
            element.SetText(value, false);
            OnPropertyChanged();
        }
    }
}

internal class VisualElementProperty(PropertyInfo propertyInfo) : ObservableObject, IValueProxy<object?>
{
    public DebuggerVisualElement? Target
    {
        get;
        set
        {
            if (field != null) field.PropertyChanged -= HandleElementPropertyChanged;
            field = value;
            if (field != null) field.PropertyChanged += HandleElementPropertyChanged;
            OnPropertyChanged(nameof(Value));
        }
    }

    private void HandleElementPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != propertyInfo.Name) return;
        OnPropertyChanged(nameof(Value));
    }

    public string Name => propertyInfo.Name;

    public bool IsReadOnly => !propertyInfo.CanWrite;

    public object? Value
    {
        get => Target == null ? null : propertyInfo.GetValue(Target);
        set
        {
            if (Target == null) return;
            if (IsReadOnly) return;
            propertyInfo.SetValue(Target, value);
        }
    }
}