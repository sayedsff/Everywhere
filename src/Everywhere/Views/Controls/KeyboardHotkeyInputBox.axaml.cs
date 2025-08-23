using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Everywhere.Common;
using Everywhere.Configuration;
using Everywhere.Interop;

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

        var hotkeyScope = ServiceLocator.Resolve<IHotkeyListener>().StartCaptureKeyboardHotkey();
        hotkeyScope.PropertyChanging += (sender, _) =>
            Dispatcher.UIThread.InvokeOnDemand(() => HotKeyTextBox.Text = sender.NotNull<IKeyboardHotkeyScope>().PressingHotkey.ToString());
        hotkeyScope.PropertyChanged += (sender, _) =>
        {
            Dispatcher.UIThread.InvokeOnDemand(() =>
            {
                Hotkey = sender.NotNull<IKeyboardHotkeyScope>().PressingHotkey;
#pragma warning disable CS0618 // 类型或成员已过时
                TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
#pragma warning restore CS0618 // 类型或成员已过时
            });
            hotkeyScope.Dispose();
        };
    }
}