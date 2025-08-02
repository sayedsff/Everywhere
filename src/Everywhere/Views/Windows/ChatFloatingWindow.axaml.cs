using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Enums;
using Everywhere.Models;
using LiveMarkdown.Avalonia;

namespace Everywhere.Views;

public partial class ChatFloatingWindow : ReactiveWindow<ChatFloatingWindowViewModel>
{
    public static readonly DirectProperty<ChatFloatingWindow, bool> IsOpenedProperty =
        AvaloniaProperty.RegisterDirect<ChatFloatingWindow, bool>(nameof(IsOpened), o => o.IsOpened);

    public bool IsOpened
    {
        get;
        private set => SetAndRaise(IsOpenedProperty, ref field, value);
    }

    public static readonly StyledProperty<PixelRect> TargetBoundingRectProperty =
        AvaloniaProperty.Register<ChatFloatingWindow, PixelRect>(nameof(TargetBoundingRect));

    public PixelRect TargetBoundingRect
    {
        get => GetValue(TargetBoundingRectProperty);
        set => SetValue(TargetBoundingRectProperty, value);
    }

    public static readonly StyledProperty<PlacementMode> PlacementProperty =
        AvaloniaProperty.Register<ChatFloatingWindow, PlacementMode>(nameof(Placement));

    public PlacementMode Placement
    {
        get => GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    public static readonly StyledProperty<bool> IsWindowPinnedProperty = AvaloniaProperty.Register<ChatFloatingWindow, bool>(
        nameof(IsWindowPinned));

    public bool IsWindowPinned
    {
        get => GetValue(IsWindowPinnedProperty);
        set => SetValue(IsWindowPinnedProperty, value);
    }

    private readonly ILauncher launcher;
    private readonly Settings settings;

    public ChatFloatingWindow(ILauncher launcher, Settings settings)
    {
        this.launcher = launcher;
        this.settings = settings;

        InitializeComponent();
        AddHandler(KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);

        ViewModel.PropertyChanged += HandleViewModelPropertyChanged;
        ChatInputBox.TextChanged += HandleChatInputBoxTextChanged;
        ChatInputBox.PastingFromClipboard += HandleChatInputBoxPastingFromClipboard;
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape when e.KeyModifiers == KeyModifiers.None:
            {
                IsOpened = false;
                break;
            }
            case Key.D when e.KeyModifiers == KeyModifiers.Control:
            {
                IsWindowPinned = !IsWindowPinned;
                break;
            }
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsVisibleProperty)
        {
            IsOpened = change.NewValue is true;
        }
        else if (change.Property == TargetBoundingRectProperty)
        {
            CalculatePositionAndPlacement();
        }
        else if (change.Property == IsWindowPinnedProperty)
        {
            var value = change.NewValue is true;
            settings.Internal.IsChatFloatingWindowPinned = value;

            if (value)
            {
                // Pin the window to the topmost level
                Topmost = false;
                Topmost = true;
            }
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);

        ClampToScreen();
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);

        if (!IsActive && !IsWindowPinned)
        {
            IsOpened = false;
        }
    }

    private void CalculatePositionAndPlacement()
    {
        // 1. Get the available area of all screens
        var actualSize = Bounds.Size.To(s => new PixelSize((int)(s.Width * DesktopScaling), (int)(s.Height * DesktopScaling)));
        if (actualSize == PixelSize.Empty)
        {
            // If the size is empty, we cannot calculate the position and placement
            return;
        }

        // 2. Screen coordinates and this window size of the target element
        var targetBoundingRectangle = TargetBoundingRect;
        if (targetBoundingRectangle.Width <= 0 || targetBoundingRectangle.Height <= 0)
        {
            // If the target bounding rectangle is invalid, we cannot calculate the position and placement
            return;
        }

        // 3. Generate a candidate list based on the priority of attachment (right → bottom → top → left) and alignment priority (top/left priority)
        var candidates = new List<(PlacementMode mode, PixelPoint pos)>
        {
            // →
            (PlacementMode.RightEdgeAlignedTop, new PixelPoint(targetBoundingRectangle.X + targetBoundingRectangle.Width, targetBoundingRectangle.Y)),
            (PlacementMode.RightEdgeAlignedBottom,
                new PixelPoint(
                    targetBoundingRectangle.X + targetBoundingRectangle.Width,
                    targetBoundingRectangle.Y + targetBoundingRectangle.Height - actualSize.Height)),

            // ↓
            (PlacementMode.BottomEdgeAlignedLeft,
                new PixelPoint(targetBoundingRectangle.X, targetBoundingRectangle.Y + targetBoundingRectangle.Height)),
            (PlacementMode.BottomEdgeAlignedRight,
                new PixelPoint(
                    targetBoundingRectangle.X + targetBoundingRectangle.Width - actualSize.Width,
                    targetBoundingRectangle.Y + targetBoundingRectangle.Height)),

            // ↑
            (PlacementMode.TopEdgeAlignedLeft, new PixelPoint(targetBoundingRectangle.X, targetBoundingRectangle.Y - actualSize.Height)),
            (PlacementMode.TopEdgeAlignedRight,
                new PixelPoint(
                    targetBoundingRectangle.X + targetBoundingRectangle.Width - actualSize.Width,
                    targetBoundingRectangle.Y - actualSize.Height)),

            // ←
            (PlacementMode.LeftEdgeAlignedTop, new PixelPoint(targetBoundingRectangle.X - actualSize.Width, targetBoundingRectangle.Y)),
            (PlacementMode.LeftEdgeAlignedBottom,
                new PixelPoint(
                    targetBoundingRectangle.X - actualSize.Width,
                    targetBoundingRectangle.Y + targetBoundingRectangle.Height - actualSize.Height)),

            // center
            (PlacementMode.Center,
                new PixelPoint(
                    targetBoundingRectangle.X + targetBoundingRectangle.Width / 2 - actualSize.Width / 2,
                    targetBoundingRectangle.Y + targetBoundingRectangle.Height / 2 - actualSize.Height / 2))
        };

        // 4. Search for the first candidate that completely falls into any screen workspace
        var screenAreas = Screens.All.Select(s => s.Bounds).ToReadOnlyList();
        foreach (var (mode, pos) in candidates)
        {
            var rect = new PixelRect(pos, actualSize);
            if (screenAreas.Any(area => area.Contains(rect)))
            {
                Position = pos;
                Placement = mode;
                return;
            }
        }

        // 5. If none of them are met, use the preferred solution and clamp it onto the main screen
        var (fallbackMode, fallbackPos) = candidates[0];
        var mainArea = screenAreas[0];
        Position = ClampToArea(fallbackPos, actualSize, mainArea);
        Placement = fallbackMode;
    }

    private void ClampToScreen()
    {
        var position = Position;
        var actualSize = Bounds.Size.To(s => new PixelSize((int)(s.Width * DesktopScaling), (int)(s.Height * DesktopScaling)));
        var screenBounds = Screens.ScreenFromPoint(position)?.Bounds ?? Screens.Primary?.Bounds ?? Screens.All[0].Bounds;
        Position = ClampToArea(position, actualSize, screenBounds);
    }

    private static PixelPoint ClampToArea(PixelPoint pos, PixelSize size, PixelRect area)
    {
        var x = Math.Max(area.X, Math.Min(pos.X, area.X + area.Width - size.Width));
        var y = Math.Max(area.Y, Math.Min(pos.Y, area.Y + area.Height - size.Height));
        return new PixelPoint(x, y);
    }

    private void HandleTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(ViewModel.IsOpened)) return;

        IsOpened = ViewModel.IsOpened;
        if (IsOpened)
        {
            Show();
            ChatInputBox.Focus();

            switch (settings.Behavior.ChatFloatingWindowPinMode)
            {
                case ChatFloatingWindowPinMode.RememberLast:
                {
                    IsWindowPinned = settings.Internal.IsChatFloatingWindowPinned;
                    break;
                }
                case ChatFloatingWindowPinMode.AlwaysPinned:
                {
                    IsWindowPinned = true;
                    break;
                }
                case ChatFloatingWindowPinMode.AlwaysUnpinned:
                case ChatFloatingWindowPinMode.PinOnInput:
                {
                    IsWindowPinned = false;
                    break;
                }
            }
        }
        else
        {
            Hide();
        }
    }

    private void HandleChatInputBoxTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (settings.Behavior.ChatFloatingWindowPinMode == ChatFloatingWindowPinMode.PinOnInput)
        {
            IsWindowPinned = true;
        }
    }

    /// <summary>
    /// TODO: Avalonia says they will support this in 12.0
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void HandleChatInputBoxPastingFromClipboard(object? sender, RoutedEventArgs e)
    {
        if (!ViewModel.AddClipboardCommand.CanExecute(null)) return;

        ViewModel.AddClipboardCommand.Execute(null);
    }

    [RelayCommand]
    private Task LaunchInlineHyperlink(InlineHyperlinkClickedEventArgs e)
    {
        // currently we only support http(s) links for safety reasons
        return e.HRef is not { Scheme: "http" or "https" } uri ? Task.CompletedTask : launcher.LaunchUriAsync(uri);
    }
}