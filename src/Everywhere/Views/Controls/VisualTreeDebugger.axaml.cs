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
using Everywhere.Enums;
using Everywhere.Models;

namespace Everywhere.Views;

public partial class VisualTreeDebugger : UserControl
{
    private readonly IVisualElementContext visualElementContext;
    private readonly IWindowHelper windowHelper;
    private readonly ObservableCollection<IVisualElement> rootElements = [];
    private readonly IReadOnlyList<VisualElementProperty> properties = typeof(DebuggerVisualElement)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Select(p => new VisualElementProperty(p))
        .ToReadOnlyList();
    private readonly Window treeViewPointerOverMask;

    public VisualTreeDebugger(
        IUserInputTrigger userInputTrigger,
        IVisualElementContext visualElementContext,
        IWindowHelper windowHelper)
    {
        this.visualElementContext = visualElementContext;
        this.windowHelper = windowHelper;

        InitializeComponent();

        VisualTreeView.ItemsSource = rootElements;
        PropertyItemsControl.ItemsSource = properties;

        userInputTrigger.KeyboardHotkeyActivated += () =>
        {
            rootElements.Clear();
            var element = visualElementContext.PointerOverElement;
            if (element == null) return;
            element = element
                .GetAncestors()
                .LastOrDefault() ?? element;
            rootElements.Add(element);
        };

        treeViewPointerOverMask = new Window
        {
            Topmost = true,
            CanResize = false,
            ShowInTaskbar = false,
            ShowActivated = false,
            SystemDecorations = SystemDecorations.None,
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
            Background = null,
            Content = new Border
            {
                Background = Brushes.DodgerBlue,
                Opacity = 0.2
            },
        };
        treeViewPointerOverMask.Closing += (_, e) => e.Cancel = true;
        windowHelper.SetWindowNoFocus(treeViewPointerOverMask);
        windowHelper.SetWindowHitTestInvisible(treeViewPointerOverMask);

        var keyboardFocusMask = new Window
        {
            Topmost = true,
            CanResize = false,
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
        keyboardFocusMask.Closing += (_, e) => e.Cancel = true;
        windowHelper.SetWindowNoFocus(keyboardFocusMask);
        windowHelper.SetWindowHitTestInvisible(keyboardFocusMask);

        visualElementContext.KeyboardFocusedElementChanged += element =>
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                if (ShowKeyboardFocusedElementCheckBox.IsChecked is true) SetMask(keyboardFocusMask, element);
            });
        };

        ShowKeyboardFocusedElementCheckBox.IsCheckedChanged += delegate
        {
            if (ShowKeyboardFocusedElementCheckBox.IsChecked is not true) SetMask(keyboardFocusMask, null);
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

        SetMask(treeViewPointerOverMask, visualElement);
    }

    private void HandleVisualTreeViewSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var debuggerElement = VisualTreeView.SelectedItem is not IVisualElement selectedItem ? null : new DebuggerVisualElement(selectedItem);
        foreach (var property in properties)
        {
            property.Target = debuggerElement;
        }
    }

    private static void SetMask(Window mask, IVisualElement? element)
    {
        if (element == null)
        {
            mask.Hide();
            mask.Topmost = false;
        }
        else
        {
            mask.Show();
            mask.Topmost = true;
            var boundingRectangle = element.BoundingRectangle;
            mask.Position = new PixelPoint(boundingRectangle.X, boundingRectangle.Y);
            mask.Width = boundingRectangle.Width / mask.DesktopScaling;
            mask.Height = boundingRectangle.Height / mask.DesktopScaling;
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
        if (TopLevel.GetTopLevel(this) is not Window window) return;

        windowHelper.HideWindowWithoutAnimation(window);

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

        window.Show();
    }

    private void HandleOptimizeButtonClicked(object? sender, RoutedEventArgs e)
    {
        if (rootElements.Count == 0) return;
        if (rootElements[0] is not OptimizedVisualElement)
        {
            rootElements[0] = new OptimizedVisualElement(rootElements[0]);
        }
    }

    private async void HandleCaptureButtonClicked(object? sender, RoutedEventArgs e)
    {
        if (VisualTreeView.SelectedItem is not IVisualElement selectedItem) return;
        try
        {
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