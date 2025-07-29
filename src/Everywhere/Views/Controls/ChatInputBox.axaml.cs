using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
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

    public static readonly StyledProperty<int> MaxChatAttachmentCountProperty =
        AvaloniaProperty.Register<ChatInputBox, int>(nameof(MaxChatAttachmentCount));

    public static readonly DirectProperty<ChatInputBox, ObservableCollection<MenuItem>> AddChatAttachmentMenuItemsProperty =
        AvaloniaProperty.RegisterDirect<ChatInputBox, ObservableCollection<MenuItem>>(
            nameof(AddChatAttachmentMenuItems),
            o => o.AddChatAttachmentMenuItems);

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

    public int MaxChatAttachmentCount
    {
        get => GetValue(MaxChatAttachmentCountProperty);
        set => SetValue(MaxChatAttachmentCountProperty, value);
    }

    public ObservableCollection<MenuItem> AddChatAttachmentMenuItems
    {
        get;
        set => SetAndRaise(AddChatAttachmentMenuItemsProperty, ref field, value);
    } = [];

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
    private IDisposable? chatAttachmentItemsControlPointerMovedSubscription;
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
        this.AddDisposableHandler(KeyDownEvent, HandleTextBoxKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        sendButtonClickSubscription?.Dispose();
        chatAttachmentItemsControlPointerMovedSubscription?.Dispose();
        attachmentItemsControlPointerExitedSubscription?.Dispose();

        // We handle the click event of the SendButton here instead of using Command binding,
        // because we need to clear the text after sending the message.
        var sendButton = e.NameScope.Find<Button>("PART_SendButton").NotNull();
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

        var chatAttachmentItemsControl = e.NameScope.Find<ItemsControl>("PART_ChatAttachmentItemsControl").NotNull();
        chatAttachmentItemsControlPointerMovedSubscription = chatAttachmentItemsControl.AddDisposableHandler(
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
        attachmentItemsControlPointerExitedSubscription = chatAttachmentItemsControl.AddDisposableHandler(
            PointerExitedEvent,
            (_, _) => visualElementAttachmentOverlayWindow.UpdateForVisualElement(null),
            handledEventsToo: true);
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

                if (Command?.CanExecute(Text) is not true) break;

                Command.Execute(Text);
                Text = string.Empty;
                e.Handled = true;
                break;
            }
        }
    }
}