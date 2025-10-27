using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Everywhere.Common;
using Everywhere.Interop;
using Everywhere.Utilities;

namespace Everywhere.Views;

public partial class KeyboardShortcutInputBox : UserControl
{
    public static readonly StyledProperty<KeyboardShortcut> ShortcutProperty =
        AvaloniaProperty.Register<KeyboardShortcutInputBox, KeyboardShortcut>(nameof(Shortcut));

    public KeyboardShortcut Shortcut
    {
        get => GetValue(ShortcutProperty);
        set => SetValue(ShortcutProperty, value);
    }

    private IKeyboardShortcutScope? _shortcutScope;

    public KeyboardShortcutInputBox()
    {
        InitializeComponent();
    }

    private void HandleClearButtonClicked(object? sender, RoutedEventArgs e)
    {
        Shortcut = default;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != ShortcutProperty) return;
        ShortcutTextBox.Text = change.NewValue switch
        {
            KeyboardShortcut { IsEmpty: false } shortcut => shortcut.ToString(),
            _ => string.Empty
        };
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);

        if (_shortcutScope is not null) return;

        ShortcutTextBox.Watermark = LocaleKey.KeyboardShortcutInputBox_SettingWatermark.I18N();

        _shortcutScope = ServiceLocator.Resolve<IShortcutListener>().StartCaptureKeyboardShortcut();
        _shortcutScope.PressingShortcutChanged += (_, hotkey) => Dispatcher.UIThread.InvokeOnDemand(() => ShortcutTextBox.Text = hotkey.ToString());
        _shortcutScope.ShortcutFinished += (_, shortcut) =>
        {
            Dispatcher.UIThread.InvokeOnDemand(() =>
            {
                Shortcut = shortcut;
                TopLevel.GetTopLevel(this)?.Focus();
            });

            DisposeCollector.DisposeToDefault(ref _shortcutScope);
        };
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);

        ShortcutTextBox.Watermark = LocaleKey.KeyboardShortcutInputBox_Watermark.I18N();

        TopLevel.GetTopLevel(this)?.Focus(); // Ensure the focus is moved away from this control.
        DisposeCollector.DisposeToDefault(ref _shortcutScope);
    }
}