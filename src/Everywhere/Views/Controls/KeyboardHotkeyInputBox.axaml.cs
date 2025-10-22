using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Everywhere.Common;
using Everywhere.Interop;
using Everywhere.Utilities;

namespace Everywhere.Views;

public partial class KeyboardHotkeyInputBox : UserControl
{
    public static readonly StyledProperty<KeyboardHotkey> HotkeyProperty =
        AvaloniaProperty.Register<KeyboardHotkeyInputBox, KeyboardHotkey>(nameof(Hotkey));

    public KeyboardHotkey Hotkey
    {
        get => GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    private IKeyboardHotkeyScope? _hotkeyScope;

    public KeyboardHotkeyInputBox()
    {
        InitializeComponent();
    }

    private void HandleClearButtonClicked(object? sender, RoutedEventArgs e)
    {
        Hotkey = default;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != HotkeyProperty) return;
        HotKeyTextBox.Text = change.NewValue switch
        {
            KeyboardHotkey { IsEmpty: false } hotkey => hotkey.ToString(),
            _ => string.Empty
        };
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);

        if (_hotkeyScope is not null) return;

        _hotkeyScope = ServiceLocator.Resolve<IHotkeyListener>().StartCaptureKeyboardHotkey();
        _hotkeyScope.PressingHotkeyChanged += (_, hotkey) => Dispatcher.UIThread.InvokeOnDemand(() => HotKeyTextBox.Text = hotkey.ToString());
        _hotkeyScope.HotkeyFinished += (_, hotkey) =>
        {
            Dispatcher.UIThread.InvokeOnDemand(() =>
            {
                Hotkey = hotkey;
                TopLevel.GetTopLevel(this)?.Focus();
            });

            DisposeCollector.DisposeToDefault(ref _hotkeyScope);
        };
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);

        TopLevel.GetTopLevel(this)?.Focus(); // Ensure the focus is moved away from this control.
        DisposeCollector.DisposeToDefault(ref _hotkeyScope);
    }
}