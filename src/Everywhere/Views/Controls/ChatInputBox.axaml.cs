using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Reactive;
using Avalonia.Utilities;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Models;

namespace Everywhere.Views;

[TemplatePart("PART_SendButton", typeof(Button))]
[TemplatePart("PART_ChatAttachmentItemsControl", typeof(ItemsControl))]
public partial class ChatInputBox : TextBox
{
    public static readonly StyledProperty<bool> PressCtrlEnterToSendProperty =
        AvaloniaProperty.Register<ChatInputBox, bool>(nameof(PressCtrlEnterToSend));

    public static readonly StyledProperty<IRelayCommand<string>?> CommandProperty =
        AvaloniaProperty.Register<ChatInputBox, IRelayCommand<string>?>(nameof(Command));

    public static readonly StyledProperty<IRelayCommand?> CancelCommandProperty =
        AvaloniaProperty.Register<ChatInputBox, IRelayCommand?>(nameof(CancelCommand));

    public static readonly StyledProperty<IList<ChatAttachment>?> ChatAttachmentItemsSourceProperty =
        AvaloniaProperty.Register<ChatInputBox, IList<ChatAttachment>?>(nameof(ChatAttachmentItemsSource));

    public static readonly DirectProperty<ChatInputBox, ObservableCollection<MenuItem>> AddChatAttachmentMenuItemsProperty =
        AvaloniaProperty.RegisterDirect<ChatInputBox, ObservableCollection<MenuItem>>(
            nameof(AddChatAttachmentMenuItems),
            o => o.AddChatAttachmentMenuItems);

    public static readonly StyledProperty<IEnumerable<ChatCommand>?> ChatCommandItemsSourceProperty =
        AvaloniaProperty.Register<ChatInputBox, IEnumerable<ChatCommand>?>(nameof(ChatCommandItemsSource));

    public static readonly StyledProperty<ChatCommand?> SelectedChatCommandProperty =
        AvaloniaProperty.Register<ChatInputBox, ChatCommand?>(nameof(SelectedChatCommand));

    public static readonly DirectProperty<ChatInputBox, bool> IsChatCommandFlyoutOpenProperty =
        AvaloniaProperty.RegisterDirect<ChatInputBox, bool>(
            nameof(IsChatCommandFlyoutOpen),
            o => o.IsChatCommandFlyoutOpen,
            (o, v) => o.IsChatCommandFlyoutOpen = v);

    public static readonly StyledProperty<bool> IsImageSupportedProperty =
        AvaloniaProperty.Register<ChatInputBox, bool>(nameof(IsImageSupported));

    public static readonly StyledProperty<bool> IsImageEnabledProperty =
        AvaloniaProperty.Register<ChatInputBox, bool>(nameof(IsImageEnabled));

    public static readonly StyledProperty<bool> IsWebSearchSupportedProperty =
        AvaloniaProperty.Register<ChatInputBox, bool>(nameof(IsWebSearchSupported));

    public static readonly StyledProperty<bool> IsWebSearchEnabledProperty =
        AvaloniaProperty.Register<ChatInputBox, bool>(nameof(IsWebSearchEnabled));

    public static readonly StyledProperty<bool> IsToolCallSupportedProperty =
        AvaloniaProperty.Register<ChatInputBox, bool>(nameof(IsToolCallSupported));

    public static readonly StyledProperty<bool> IsToolCallEnabledProperty =
        AvaloniaProperty.Register<ChatInputBox, bool>(nameof(IsToolCallEnabled));

    public static readonly StyledProperty<bool> IsSendButtonEnabledProperty =
        AvaloniaProperty.Register<ChatInputBox, bool>(nameof(IsSendButtonEnabled), true);

    /// <summary>
    /// If true, pressing Ctrl+Enter will send the message, Enter will break the line.
    /// </summary>
    public bool PressCtrlEnterToSend
    {
        get => GetValue(PressCtrlEnterToSendProperty);
        set => SetValue(PressCtrlEnterToSendProperty, value);
    }

    /// <summary>
    /// When the text is executed, the text will be passed as the parameter.
    /// </summary>
    public IRelayCommand<string>? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public IRelayCommand? CancelCommand
    {
        get => GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    public IList<ChatAttachment>? ChatAttachmentItemsSource
    {
        get => GetValue(ChatAttachmentItemsSourceProperty);
        set => SetValue(ChatAttachmentItemsSourceProperty, value);
    }

    public ObservableCollection<MenuItem> AddChatAttachmentMenuItems
    {
        get;
        set => SetAndRaise(AddChatAttachmentMenuItemsProperty, ref field, value);
    } = [];

    public IEnumerable<ChatCommand>? ChatCommandItemsSource
    {
        get => GetValue(ChatCommandItemsSourceProperty);
        set => SetValue(ChatCommandItemsSourceProperty, value);
    }

    public ChatCommand? SelectedChatCommand
    {
        get => GetValue(SelectedChatCommandProperty);
        set => SetValue(SelectedChatCommandProperty, value);
    }

    public bool IsChatCommandFlyoutOpen
    {
        get;
        private set => SetAndRaise(IsChatCommandFlyoutOpenProperty, ref field, value);
    }

    public bool IsImageSupported
    {
        get => GetValue(IsImageSupportedProperty);
        set => SetValue(IsImageSupportedProperty, value);
    }

    public bool IsImageEnabled
    {
        get => GetValue(IsImageEnabledProperty);
        set => SetValue(IsImageEnabledProperty, value);
    }

    public bool IsWebSearchSupported
    {
        get => GetValue(IsWebSearchSupportedProperty);
        set => SetValue(IsWebSearchSupportedProperty, value);
    }

    public bool IsWebSearchEnabled
    {
        get => GetValue(IsWebSearchEnabledProperty);
        set => SetValue(IsWebSearchEnabledProperty, value);
    }

    public bool IsToolCallSupported
    {
        get => GetValue(IsToolCallSupportedProperty);
        set => SetValue(IsToolCallSupportedProperty, value);
    }

    public bool IsToolCallEnabled
    {
        get => GetValue(IsToolCallEnabledProperty);
        set => SetValue(IsToolCallEnabledProperty, value);
    }

    public bool IsSendButtonEnabled
    {
        get => GetValue(IsSendButtonEnabledProperty);
        set => SetValue(IsSendButtonEnabledProperty, value);
    }

    private IDisposable? sendButtonClickSubscription;
    private IDisposable? attachmentItemsControlPointerMovedSubscription;
    private IDisposable? attachmentItemsControlPointerExitedSubscription;

    private readonly OverlayWindow visualElementAttachmentOverlayWindow = new()
    {
        Content = new Border
        {
            Background = Brushes.DodgerBlue,
            Opacity = 0.2
        },
    };

    static ChatInputBox()
    {
        TextProperty.OverrideDefaultValue<ChatInputBox>(string.Empty);
    }

    public ChatInputBox()
    {
        SelectedChatCommandProperty.Changed.Subscribe(
            new AnonymousObserver<AvaloniaPropertyChangedEventArgs<ChatCommand?>>(HandleSelectedChatCommandPropertyChanged));
        this.AddDisposableHandler(KeyDownEvent, HandleTextBoxKeyDown, RoutingStrategies.Tunnel);
        this.AddDisposableHandler(TextChangedEvent, HandleTextBoxTextChanged, RoutingStrategies.Bubble);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        sendButtonClickSubscription?.Dispose();
        attachmentItemsControlPointerMovedSubscription?.Dispose();
        attachmentItemsControlPointerExitedSubscription?.Dispose();

        if (e.NameScope.Find<Button>("PART_SendButton") is { } sendButton)
        {
            sendButtonClickSubscription = sendButton.AddDisposableHandler(
                Button.ClickEvent,
                (_, args) =>
                {
                    if (Command?.CanExecute(Text) is not true) return;

                    Command.Execute(Text);
                    Text = string.Empty;
                    args.Handled = true;
                },
                handledEventsToo: true);
        }
        if (e.NameScope.Find<ItemsControl>("PART_AttachmentItemsControl") is { } attachmentItemsControl)
        {
            attachmentItemsControlPointerMovedSubscription = attachmentItemsControl.AddDisposableHandler(
                PointerMovedEvent,
                (_, args) =>
                {
                    var element = args.Source as StyledElement;
                    while (element != null)
                    {
                        element = element.Parent;
                        if (element is not { DataContext: ChatVisualElementAttachment attachment }) continue;
                        visualElementAttachmentOverlayWindow.UpdateForVisualElement(attachment.Element);
                        return;
                    }
                    visualElementAttachmentOverlayWindow.UpdateForVisualElement(null);
                },
                handledEventsToo: true);
            attachmentItemsControlPointerExitedSubscription = attachmentItemsControl.AddDisposableHandler(
                PointerExitedEvent,
                (_, _) => visualElementAttachmentOverlayWindow.UpdateForVisualElement(null),
                handledEventsToo: true);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        // Because this control is inherited from TextBox, it will receive pointer events and broke the MenuItem's pointer events.
        // We need to ignore pointer events if the source is a StyledElement that is inside a MenuItem.
        if (e.Source is StyledElement element && element.FindLogicalAncestorOfType<MenuItem>() != null)
        {
            return;
        }

        base.OnPointerPressed(e);
    }

    [RelayCommand]
    private void RemoveAttachment(ChatAttachment attachment)
    {
        ChatAttachmentItemsSource?.Remove(attachment);
    }

    private void HandleTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
            {
                if ((!PressCtrlEnterToSend || e.KeyModifiers != KeyModifiers.Control) &&
                    (PressCtrlEnterToSend || e.KeyModifiers != KeyModifiers.None)) return;

                if (Command?.CanExecute(Text) is true) Command.Execute(Text);
                Text = string.Empty;
                e.Handled = true;
                break;
            }
        }
    }

    private void HandleSelectedChatCommandPropertyChanged(AvaloniaPropertyChangedEventArgs<ChatCommand?> e)
    {
        if (e.NewValue.Value is not { } command) return;
        var text = Text;
        if (text != null && text.StartsWith('/'))
        {
            text = text.IndexOf(' ') is var i and >= 0 ? text[i..] : string.Empty;
        }
        Text = command.Command + ' ' + text?.TrimStart();
        CaretIndex = Text.Length;
    }

    private void HandleTextBoxTextChanged(object? sender, TextChangedEventArgs e)
    {
        var text = Text;
        if (text == null) return;

        if (SelectedChatCommand != null && text.StartsWith(SelectedChatCommand.Command + ' ') is not true)
        {
            SelectedChatCommand = null;
            if (text.StartsWith('/'))
            {
                Text = text = text.IndexOf(' ') is var i and >= 0 ? text[i..] : string.Empty;
            }
        }

        IsChatCommandFlyoutOpen = text.StartsWith('/') && SelectedChatCommand == null;
    }
}

public class ChatInputTextPresenter : TextPresenter
{
    public static readonly StyledProperty<ChatCommand?> SelectedChatCommandProperty =
        ChatInputBox.SelectedChatCommandProperty.AddOwner<ChatInputTextPresenter>();

    public ChatCommand? SelectedChatCommand
    {
        get => GetValue(SelectedChatCommandProperty);
        set => SetValue(SelectedChatCommandProperty, value);
    }

    private Size constraint;

    public ChatInputTextPresenter()
    {
        SelectedChatCommandProperty.Changed.Subscribe(
            new AnonymousObserver<AvaloniaPropertyChangedEventArgs<ChatCommand?>>(_ => InvalidateTextLayout()));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        constraint = availableSize;
        return base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var finalWidth = finalSize.Width;
        if (!constraint.Width.IsCloseTo(finalWidth))
        {
            constraint = new Size(Math.Ceiling(finalWidth), double.PositiveInfinity);
        }

        return finalSize;
    }

    protected override TextLayout CreateTextLayout()
    {
        var text = Text;
        var command = SelectedChatCommand?.Command;
        var selectionStart = SelectionStart;
        var selectionEnd = SelectionEnd;
        var foreground = Foreground;

        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var start = Math.Min(selectionStart, selectionEnd);
        var length = Math.Max(selectionStart, selectionEnd) - start;

        IReadOnlyList<ValueSpan<TextRunProperties>>? textStyleOverrides = null;
        if (text != null && command != null && text.StartsWith(command + ' '))
        {
            textStyleOverrides =
            [
                new ValueSpan<TextRunProperties>(
                    0,
                    command.Length,
                    new GenericTextRunProperties(
                        typeface,
                        FontFeatures,
                        FontSize,
                        foregroundBrush: foreground,
                        textDecorations: TextDecorations.Underline))
            ];
        }
        else if (ShowSelectionHighlight && length > 0 && SelectionForegroundBrush != null)
        {
            textStyleOverrides =
            [
                new ValueSpan<TextRunProperties>(
                    start,
                    length,
                    new GenericTextRunProperties(
                        typeface,
                        FontFeatures,
                        FontSize,
                        foregroundBrush: SelectionForegroundBrush)),
            ];
        }

        var maxWidth = constraint.Width.IsCloseTo(0d) ? double.PositiveInfinity : constraint.Width;
        var maxHeight = constraint.Height.IsCloseTo(0d) ? double.PositiveInfinity : constraint.Height;
        var textLayout = new TextLayout(
            text,
            typeface,
            FontFeatures,
            FontSize,
            foreground,
            TextAlignment,
            TextWrapping,
            maxWidth: maxWidth,
            maxHeight: maxHeight,
            textStyleOverrides: textStyleOverrides,
            flowDirection: FlowDirection,
            lineHeight: LineHeight,
            letterSpacing: LetterSpacing);
        return textLayout;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == PreeditTextProperty) return;

        base.OnPropertyChanged(change);
    }
}