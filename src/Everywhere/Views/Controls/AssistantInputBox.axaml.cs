using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Reactive;
using Avalonia.Utilities;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Models;

namespace Everywhere.Views;

[TemplatePart("PART_SendButton", typeof(Button))]
public class AssistantInputBox : TextBox
{
    public static readonly StyledProperty<bool> PressCtrlEnterToSendProperty =
        AvaloniaProperty.Register<AssistantInputBox, bool>(nameof(PressCtrlEnterToSend));

    public static readonly StyledProperty<IRelayCommand<string>?> CommandProperty =
        AvaloniaProperty.Register<AssistantInputBox, IRelayCommand<string>?>(nameof(Command));

    public static readonly StyledProperty<IList<AssistantAttachment>> AttachmentItemsSourceProperty =
        AvaloniaProperty.Register<AssistantInputBox, IList<AssistantAttachment>>(nameof(AttachmentItemsSource));

    public static readonly StyledProperty<IEnumerable> AddAttachmentCommandItemsSourceProperty =
        AvaloniaProperty.Register<AssistantInputBox, IEnumerable>(nameof(AddAttachmentCommandItemsSource));

    public static readonly StyledProperty<IEnumerable<AssistantCommand>> AssistantCommandItemsSourceProperty =
        AvaloniaProperty.Register<AssistantInputBox, IEnumerable<AssistantCommand>>(nameof(AssistantCommandItemsSource));

    public static readonly StyledProperty<AssistantCommand?> SelectedAssistantCommandProperty =
        AvaloniaProperty.Register<AssistantInputBox, AssistantCommand?>(nameof(SelectedAssistantCommand));

    public static readonly DirectProperty<AssistantInputBox, bool> IsAssistantCommandFlyoutOpenProperty =
        AvaloniaProperty.RegisterDirect<AssistantInputBox, bool>(
            nameof(IsAssistantCommandFlyoutOpen),
            o => o.IsAssistantCommandFlyoutOpen,
            (o, v) => o.IsAssistantCommandFlyoutOpen = v);

    public static readonly StyledProperty<bool> IsImageSupportedProperty =
        AvaloniaProperty.Register<AssistantInputBox, bool>(nameof(IsImageSupported));

    public static readonly StyledProperty<bool> IsImageEnabledProperty =
        AvaloniaProperty.Register<AssistantInputBox, bool>(nameof(IsImageEnabled));

    public static readonly StyledProperty<bool> IsWebSearchSupportedProperty =
        AvaloniaProperty.Register<AssistantInputBox, bool>(nameof(IsWebSearchSupported));

    public static readonly StyledProperty<bool> IsWebSearchEnabledProperty =
        AvaloniaProperty.Register<AssistantInputBox, bool>(nameof(IsWebSearchEnabled));

    public static readonly StyledProperty<bool> IsToolCallSupportedProperty =
        AvaloniaProperty.Register<AssistantInputBox, bool>(nameof(IsToolCallSupported));

    public static readonly StyledProperty<bool> IsToolCallEnabledProperty =
        AvaloniaProperty.Register<AssistantInputBox, bool>(nameof(IsToolCallEnabled));

    public static readonly StyledProperty<bool> IsSendButtonEnabledProperty =
        AvaloniaProperty.Register<AssistantInputBox, bool>(nameof(IsSendButtonEnabled), true);

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

    public IList<AssistantAttachment> AttachmentItemsSource
    {
        get => GetValue(AttachmentItemsSourceProperty);
        set => SetValue(AttachmentItemsSourceProperty, value);
    }

    public IEnumerable AddAttachmentCommandItemsSource
    {
        get => GetValue(AddAttachmentCommandItemsSourceProperty);
        set => SetValue(AddAttachmentCommandItemsSourceProperty, value);
    }

    public IEnumerable<AssistantCommand> AssistantCommandItemsSource
    {
        get => GetValue(AssistantCommandItemsSourceProperty);
        set => SetValue(AssistantCommandItemsSourceProperty, value);
    }

    public AssistantCommand? SelectedAssistantCommand
    {
        get => GetValue(SelectedAssistantCommandProperty);
        set => SetValue(SelectedAssistantCommandProperty, value);
    }

    public bool IsAssistantCommandFlyoutOpen
    {
        get;
        private set => SetAndRaise(IsAssistantCommandFlyoutOpenProperty, ref field, value);
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

    static AssistantInputBox()
    {
        TextProperty.OverrideDefaultValue<AssistantInputBox>(string.Empty);
    }

    public AssistantInputBox()
    {
        SelectedAssistantCommandProperty.Changed.Subscribe(
            new AnonymousObserver<AvaloniaPropertyChangedEventArgs<AssistantCommand?>>(HandleSelectedAssistantCommandPropertyChanged));
        this.AddDisposableHandler(KeyDownEvent, HandleTextBoxKeyDown, RoutingStrategies.Tunnel);
        this.AddDisposableHandler(TextChangedEvent, HandleTextBoxTextChanged, RoutingStrategies.Bubble);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        sendButtonClickSubscription?.Dispose();

        if (e.NameScope.Find<Button>("PART_SendButton") is { } sendButton)
        {
            sendButtonClickSubscription = sendButton.AddDisposableHandler(Button.ClickEvent, HandleSendButtonClick, handledEventsToo: true);
        }
    }

    private void Remove()
    {

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

    private void HandleSelectedAssistantCommandPropertyChanged(AvaloniaPropertyChangedEventArgs<AssistantCommand?> e)
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

        if (SelectedAssistantCommand != null && text.StartsWith(SelectedAssistantCommand.Command + ' ') is not true)
        {
            SelectedAssistantCommand = null;
            if (text.StartsWith('/'))
            {
                Text = text = text.IndexOf(' ') is var i and >= 0 ? text[i..] : string.Empty;
            }
        }

        IsAssistantCommandFlyoutOpen = text.StartsWith('/') && SelectedAssistantCommand == null;
    }

    private void HandleSendButtonClick(object? sender, RoutedEventArgs e)
    {
        if (Command?.CanExecute(Text) is not true) return;

        Command.Execute(Text);
        Text = string.Empty;
        e.Handled = true;
    }
}

public class AssistantInputTextPresenter : TextPresenter
{
    public static readonly StyledProperty<AssistantCommand?> SelectedAssistantCommandProperty =
        AssistantInputBox.SelectedAssistantCommandProperty.AddOwner<AssistantInputTextPresenter>();

    public AssistantCommand? SelectedAssistantCommand
    {
        get => GetValue(SelectedAssistantCommandProperty);
        set => SetValue(SelectedAssistantCommandProperty, value);
    }

    private Size constraint;

    public AssistantInputTextPresenter()
    {
        SelectedAssistantCommandProperty.Changed.Subscribe(
            new AnonymousObserver<AvaloniaPropertyChangedEventArgs<AssistantCommand?>>(
                _ => InvalidateTextLayout()));
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
        var command = SelectedAssistantCommand?.Command;
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
        var textLayout = new TextLayout(text, typeface, FontFeatures, FontSize, foreground, TextAlignment,
            TextWrapping, maxWidth: maxWidth, maxHeight: maxHeight, textStyleOverrides: textStyleOverrides,
            flowDirection: FlowDirection, lineHeight: LineHeight, letterSpacing: LetterSpacing);
        return textLayout;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == PreeditTextProperty) return;

        base.OnPropertyChanged(change);
    }
}