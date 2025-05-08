using System.Collections.Specialized;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Reactive;
using CommunityToolkit.Mvvm.Input;

namespace Everywhere.Views;

[TemplatePart("PART_TextBox", typeof(TextBox))]
[TemplatePart("PART_SendButton", typeof(Button))]
public class AssistantInputBox : ItemsControl
{
    public static readonly StyledProperty<string?> TextProperty =
        TextBox.TextProperty.AddOwner<AssistantInputBox>();

    public static readonly StyledProperty<string?> WatermarkProperty =
        TextBox.WatermarkProperty.AddOwner<AssistantInputBox>();

    public static readonly StyledProperty<bool> PressCtrlEnterToSendProperty =
        AvaloniaProperty.Register<AssistantInputBox, bool>(nameof(PressCtrlEnterToSend));

    public static readonly StyledProperty<IRelayCommand<string>?> CommandProperty =
        AvaloniaProperty.Register<AssistantInputBox, IRelayCommand<string>?>(nameof(Command));

    public static readonly StyledProperty<IEnumerable> AddableAttachmentItemsSourceProperty =
        AvaloniaProperty.Register<AssistantInputBox, IEnumerable>(nameof(AddableAttachmentItemsSource));

    public static readonly StyledProperty<bool> IsImageEnabledProperty =
        AvaloniaProperty.Register<AssistantInputBox, bool>(nameof(IsImageEnabled));

    public static readonly StyledProperty<bool> IsWebSearchEnabledProperty =
        AvaloniaProperty.Register<AssistantInputBox, bool>(nameof(IsWebSearchEnabled));

    public static readonly StyledProperty<bool> IsToolCallEnabledProperty =
        AvaloniaProperty.Register<AssistantInputBox, bool>(nameof(IsToolCallEnabled));

    public static readonly StyledProperty<bool> IsSendButtonEnabledProperty =
        AvaloniaProperty.Register<AssistantInputBox, bool>(nameof(IsSendButtonEnabled), true);

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    public bool PressCtrlEnterToSend
    {
        get => GetValue(PressCtrlEnterToSendProperty);
        set => SetValue(PressCtrlEnterToSendProperty, value);
    }

    public IRelayCommand<string>? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public IEnumerable AddableAttachmentItemsSource
    {
        get => GetValue(AddableAttachmentItemsSourceProperty);
        set => SetValue(AddableAttachmentItemsSourceProperty, value);
    }

    public AvaloniaList<DynamicKeyMenuItem> AddableAttachmentItems { get; } = [];

    public bool IsImageEnabled
    {
        get => GetValue(IsImageEnabledProperty);
        set => SetValue(IsImageEnabledProperty, value);
    }

    public bool IsWebSearchEnabled
    {
        get => GetValue(IsWebSearchEnabledProperty);
        set => SetValue(IsWebSearchEnabledProperty, value);
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

    private IDisposable? textBoxKeyDownSubscription;
    private IDisposable? sendButtonClickSubscription;

    static AssistantInputBox()
    {
        TextProperty.OverrideDefaultValue<AssistantInputBox>(string.Empty);
    }

    public AssistantInputBox()
    {
        AddableAttachmentItemsSourceProperty.Changed.Subscribe(new AnonymousObserver<AvaloniaPropertyChangedEventArgs<IEnumerable>>(
            HandleAddableAttachmentItemsSourceChanged));

        void HandleAddableAttachmentItemsSourceChanged(AvaloniaPropertyChangedEventArgs<IEnumerable> e)
        {
            if (e.OldValue is { HasValue: true, Value: INotifyCollectionChanged oldCollection })
            {
                oldCollection.CollectionChanged -= OnAddableAttachmentItemsSourceChanged;
            }
            if (e.NewValue is { HasValue: true, Value: INotifyCollectionChanged newCollection })
            {
                newCollection.CollectionChanged += OnAddableAttachmentItemsSourceChanged;
                foreach (var newItem in e.NewValue.Value) AddItem(newItem);
            }

            void OnAddableAttachmentItemsSourceChanged(object? sender, NotifyCollectionChangedEventArgs args)
            {
                switch (args.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                    {
                        foreach (var newItem in args.NewItems!) AddItem(newItem);
                        break;
                    }
                    case NotifyCollectionChangedAction.Remove:
                    {
                        foreach (var oldItem in args.OldItems!) RemoveItem(oldItem);
                        break;
                    }
                    case NotifyCollectionChangedAction.Reset:
                    {
                        AddableAttachmentItems.Clear();
                        break;
                    }
                }
            }

            void AddItem(object item)
            {
                switch (item)
                {
                    case DynamicKeyMenuItem assistantAttachmentItem:
                    {
                        AddableAttachmentItems.Add(assistantAttachmentItem);
                        break;
                    }
                    case MenuItem menuItem:
                    {
                        var newItem = new DynamicKeyMenuItem
                        {
                            Header = menuItem.Header,
                            Icon = menuItem.Icon,
                            Command = menuItem.Command,
                            CommandParameter = menuItem.CommandParameter,
                            IsEnabled = menuItem.IsEnabled,
                            IsChecked = menuItem.IsChecked
                        };
                        AddableAttachmentItems.Add(newItem);
                        break;
                    }
                    default:
                    {
                        AddableAttachmentItems.Add(
                            new DynamicKeyMenuItem
                            {
                                Header = item
                            });
                        break;
                    }
                }
            }

            void RemoveItem(object item)
            {
                switch (item)
                {
                    case DynamicKeyMenuItem assistantAttachmentItem:
                    {
                        AddableAttachmentItems.Remove(assistantAttachmentItem);
                        break;
                    }
                    case MenuItem menuItem:
                    {
                        AddableAttachmentItems.RemoveWhere(x => Equals(x.Header, menuItem.Header));
                        break;
                    }
                    default:
                    {
                        AddableAttachmentItems.RemoveWhere(x => Equals(x.Header, item));
                        break;
                    }
                }
            }
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        textBoxKeyDownSubscription?.Dispose();
        sendButtonClickSubscription?.Dispose();

        if (e.NameScope.Find<TextBox>("PART_TextBox") is { } textBox)
        {
            textBoxKeyDownSubscription = textBox.AddDisposableHandler(KeyDownEvent, TextBoxKeyDown, RoutingStrategies.Tunnel);
        }
        if (e.NameScope.Find<Button>("PART_SendButton") is { } sendButton)
        {
            sendButtonClickSubscription = sendButton.AddDisposableHandler(Button.ClickEvent, SendButtonClick, RoutingStrategies.Tunnel);
        }
    }

    private void TextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Command?.CanExecute(Text) is not true) return;

        if ((!PressCtrlEnterToSend || e.KeyModifiers != KeyModifiers.Control) &&
            (PressCtrlEnterToSend || e.KeyModifiers != KeyModifiers.None)) return;

        Command.Execute(Text);
        Text = string.Empty;
        e.Handled = true;
    }

    private void SendButtonClick(object? sender, RoutedEventArgs e)
    {
        if (Command?.CanExecute(Text) is not true) return;

        Command.Execute(Text);
        Text = string.Empty;
        e.Handled = true;
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
    {
        return new MenuItem();
    }

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        return NeedsContainer<MenuItem>(item, out recycleKey);
    }
}