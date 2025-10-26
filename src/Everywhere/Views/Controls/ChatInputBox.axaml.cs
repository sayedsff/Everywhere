using System.Collections.Specialized;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Everywhere.AI;
using Everywhere.Chat;
using Everywhere.Utilities;

namespace Everywhere.Views;

[TemplatePart("PART_SendButton", typeof(Button), IsRequired = true)]
[TemplatePart("PART_ChatAttachmentItemsControl", typeof(ItemsControl), IsRequired = true)]
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

    public static readonly StyledProperty<IEnumerable<CustomAssistant>?> CustomAssistantsProperty =
        AvaloniaProperty.Register<ChatInputBox, IEnumerable<CustomAssistant>?>(nameof(CustomAssistants));

    public static readonly StyledProperty<CustomAssistant?> SelectedCustomAssistantProperty =
        AvaloniaProperty.Register<ChatInputBox, CustomAssistant?>(nameof(SelectedCustomAssistant));

    public static readonly DirectProperty<ChatInputBox, IEnumerable?> AddChatAttachmentMenuItemsProperty =
        AvaloniaProperty.RegisterDirect<ChatInputBox, IEnumerable?>(
            nameof(AddChatAttachmentMenuItems),
            o => o.AddChatAttachmentMenuItems);

    public static readonly DirectProperty<ChatInputBox, IEnumerable?> SettingsMenuItemsProperty =
        AvaloniaProperty.RegisterDirect<ChatInputBox, IEnumerable?>(
            nameof(SettingsMenuItems),
            o => o.SettingsMenuItems);

    public static readonly StyledProperty<bool> IsImageSupportedProperty =
        AvaloniaProperty.Register<ChatInputBox, bool>(nameof(IsImageSupported));

    public static readonly StyledProperty<bool> IsImageEnabledProperty =
        AvaloniaProperty.Register<ChatInputBox, bool>(nameof(IsImageEnabled));

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

    public CustomAssistant? SelectedCustomAssistant
    {
        get => GetValue(SelectedCustomAssistantProperty);
        set => SetValue(SelectedCustomAssistantProperty, value);
    }

    public IEnumerable<CustomAssistant>? CustomAssistants
    {
        get => GetValue(CustomAssistantsProperty);
        set => SetValue(CustomAssistantsProperty, value);
    }

    public IEnumerable? AddChatAttachmentMenuItems
    {
        get;
        set => SetAndRaise(AddChatAttachmentMenuItemsProperty, ref field, value);
    } = new AvaloniaList<MenuItem>();

    public IEnumerable? SettingsMenuItems
    {
        get;
        set => SetAndRaise(SettingsMenuItemsProperty, ref field, value);
    } = new AvaloniaList<object>();

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

    private IDisposable? _textChangedSubscription;
    private IDisposable? _sendButtonClickSubscription;
    private IDisposable? _textPresenterSizeChangedSubscription;
    private IDisposable? _chatAttachmentItemsControlPointerMovedSubscription;
    private IDisposable? _chatAttachmentItemsControlPointerExitedSubscription;

    private readonly Lazy<OverlayWindow> _visualElementAttachmentOverlayWindow;

    static ChatInputBox()
    {
        TextProperty.OverrideDefaultValue<ChatInputBox>(string.Empty);
    }

    public ChatInputBox()
    {
        _visualElementAttachmentOverlayWindow = new Lazy<OverlayWindow>(
            () => new OverlayWindow(TopLevel.GetTopLevel(this) as WindowBase)
            {
                Content = new Border
                {
                    Background = Brushes.DodgerBlue,
                    Opacity = 0.2
                },
            },
            LazyThreadSafetyMode.None);

        this.AddDisposableHandler(KeyDownEvent, HandleTextBoxKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        DisposeCollector.DisposeToDefault(ref _textChangedSubscription);
        DisposeCollector.DisposeToDefault(ref _sendButtonClickSubscription);
        DisposeCollector.DisposeToDefault(ref _textPresenterSizeChangedSubscription);
        DisposeCollector.DisposeToDefault(ref _chatAttachmentItemsControlPointerMovedSubscription);
        DisposeCollector.DisposeToDefault(ref _chatAttachmentItemsControlPointerExitedSubscription);

        // We handle the click event of the SendButton here instead of using Command binding,
        // because we need to clear the text after sending the message.
        var sendButton = e.NameScope.Find<Button>("PART_SendButton").NotNull();
        _sendButtonClickSubscription = sendButton.AddDisposableHandler(
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
        _chatAttachmentItemsControlPointerMovedSubscription = chatAttachmentItemsControl.AddDisposableHandler(
            PointerMovedEvent,
            (_, args) =>
            {
                var element = args.Source as StyledElement;
                while (element != null)
                {
                    element = element.Parent;
                    if (element is not { DataContext: ChatVisualElementAttachment attachment }) continue;
                    _visualElementAttachmentOverlayWindow.Value.UpdateForVisualElement(attachment.Element?.Target);
                    return;
                }
                _visualElementAttachmentOverlayWindow.Value.UpdateForVisualElement(null);
            },
            handledEventsToo: true);
        _chatAttachmentItemsControlPointerExitedSubscription = chatAttachmentItemsControl.AddDisposableHandler(
            PointerExitedEvent,
            (_, _) => _visualElementAttachmentOverlayWindow.Value.UpdateForVisualElement(null),
            handledEventsToo: true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ChatAttachmentItemsSourceProperty)
        {
            if (change.OldValue is INotifyCollectionChanged oldValue)
            {
                oldValue.CollectionChanged -= HandleChatAttachmentItemsSourceChanged;
            }
            if (change.NewValue is INotifyCollectionChanged newValue)
            {
                newValue.CollectionChanged += HandleChatAttachmentItemsSourceChanged;
            }
        }
    }

    private void HandleChatAttachmentItemsSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_visualElementAttachmentOverlayWindow.IsValueCreated)
        {
            _visualElementAttachmentOverlayWindow.Value.UpdateForVisualElement(null); // Hide the overlay window when the attachment list changes.
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        if (_visualElementAttachmentOverlayWindow.IsValueCreated)
        {
            _visualElementAttachmentOverlayWindow.Value.UpdateForVisualElement(null); // Hide the overlay window when the control is unloaded.
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
    private void SetSelectedCustomAssistant(MenuItem? sender)
    {
        SelectedCustomAssistant = sender?.DataContext as CustomAssistant;
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