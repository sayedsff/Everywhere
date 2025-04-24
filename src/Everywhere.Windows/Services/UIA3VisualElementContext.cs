using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Avalonia;
using Everywhere.Enums;
using Everywhere.Interfaces;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace Everywhere.Windows.Services;

// ReSharper disable once InconsistentNaming
public class UIA3VisualElementContext : IVisualElementContext
{
    private static readonly UIA3Automation Automation = new();
    private static readonly ITreeWalker TreeWalker = Automation.TreeWalkerFactory.GetRawViewWalker();

    public IVisualElement? KeyboardFocusedElement => TryFrom(Automation.FocusedElement);

    public IVisualElement? PointerOverElement => TryFrom(
        static () => PInvoke.GetCursorPos(out var point) ? Automation.FromPoint(point) : null);

    private static UIAutomationVisualElement? TryFrom(Func<AutomationElement?> factory)
    {
        try
        {
            if (factory() is { } element) return new UIAutomationVisualElement(element);
        }
        catch (Exception ex)
        {
            // Log the exception if needed
            Console.WriteLine($"Error retrieving UI Automation element: {ex.Message}");
        }

        return null;
    }

    private class UIAutomationVisualElement(AutomationElement element) : IVisualElement
    {
        public IVisualElement? Parent
        {
            get
            {
                var parent = TreeWalker.GetParent(element);
                return parent is null ? null : new UIAutomationVisualElement(parent);
            }
        }

        public IEnumerable<IVisualElement> Children
        {
            get
            {
                var child = TreeWalker.GetFirstChild(element);
                while (child is not null)
                {
                    yield return new UIAutomationVisualElement(child);
                    child = TreeWalker.GetNextSibling(child);
                }
            }
        }

        public VisualElementType Type => element.Properties.ControlType.ValueOrDefault switch
        {
            ControlType.AppBar => VisualElementType.Menu,
            ControlType.Button => VisualElementType.Button,
            ControlType.Calendar => VisualElementType.TextBlock,
            ControlType.CheckBox => VisualElementType.CheckBox,
            ControlType.ComboBox => VisualElementType.ComboBox,
            ControlType.DataGrid => VisualElementType.DataGrid,
            ControlType.DataItem => VisualElementType.DataGridItem,
            ControlType.Document => VisualElementType.Document,
            ControlType.Edit => VisualElementType.TextBox,
            ControlType.Group => VisualElementType.Panel,
            ControlType.Header => VisualElementType.TableRow,
            ControlType.HeaderItem => VisualElementType.TableRow,
            ControlType.Hyperlink => VisualElementType.Hyperlink,
            ControlType.Image => VisualElementType.Image,
            ControlType.List => VisualElementType.ListView,
            ControlType.ListItem => VisualElementType.ListViewItem,
            ControlType.Menu => VisualElementType.Menu,
            ControlType.MenuBar => VisualElementType.Menu,
            ControlType.MenuItem => VisualElementType.MenuItem,
            ControlType.Pane => VisualElementType.TopLevel,
            ControlType.ProgressBar => VisualElementType.ProgressBar,
            ControlType.RadioButton => VisualElementType.RadioButton,
            ControlType.ScrollBar => VisualElementType.ScrollBar,
            ControlType.SemanticZoom => VisualElementType.ListView,
            ControlType.Separator => VisualElementType.Unknown,
            ControlType.Slider => VisualElementType.Slider,
            ControlType.Spinner => VisualElementType.Slider,
            ControlType.SplitButton => VisualElementType.Button,
            ControlType.StatusBar => VisualElementType.Panel,
            ControlType.Tab => VisualElementType.TabControl,
            ControlType.TabItem => VisualElementType.TabItem,
            ControlType.Table => VisualElementType.Table,
            ControlType.Text => VisualElementType.TextBlock,
            ControlType.Thumb => VisualElementType.Slider,
            ControlType.TitleBar => VisualElementType.Panel,
            ControlType.ToolBar => VisualElementType.Panel,
            ControlType.ToolTip => VisualElementType.Panel,
            ControlType.Tree => VisualElementType.TreeView,
            ControlType.TreeItem => VisualElementType.TreeViewItem,
            ControlType.Window => VisualElementType.TopLevel,
            _ => VisualElementType.Unknown
        };

        public VisualElementStates States
        {
            get
            {
                var states = VisualElementStates.None;
                if (element.Properties.IsOffscreen.ValueOrDefault) states |= VisualElementStates.Offscreen;
                if (!element.Properties.IsEnabled.ValueOrDefault) states |= VisualElementStates.Disabled;
                if (element.Properties.HasKeyboardFocus.ValueOrDefault) states |= VisualElementStates.Focused;
                if (element.Patterns.SelectionItem.PatternOrDefault is { IsSelected.ValueOrDefault: true }) states |= VisualElementStates.Selected;
                if (element.Patterns.Value.PatternOrDefault is { IsReadOnly.ValueOrDefault: true }) states |= VisualElementStates.ReadOnly;
                if (element.Properties.IsPassword.ValueOrDefault) states |= VisualElementStates.Password;
                return states;
            }
        }

        public string? Name => element.Properties.Name.ValueOrDefault;

        public string? Text
        {
            get
            {
                if (element.Patterns.Value.PatternOrDefault is { } valuePattern) return valuePattern.Value;
                if (element.Patterns.Text.PatternOrDefault is { } textPattern) return textPattern.DocumentRange.GetText(-1);
                return null;
            }
            set
            {
                if (States.HasFlag(VisualElementStates.Disabled | VisualElementStates.ReadOnly))
                {
                    throw new InvalidOperationException("Cannot set text on a disabled or read-only element.");
                }

                _ = TrySetValueWithValuePattern() ||
                    TrySetValueWithSendInput();

                bool TrySetValueWithValuePattern()
                {
                    if (element.Patterns.Value.PatternOrDefault is not { } valuePattern ||
                        valuePattern.IsReadOnly) return false;

                    element.Focus();
                    valuePattern.SetValue(value);
                    return true;
                }

                bool TrySetValueWithSendInput()
                {
                    if (element.FrameworkAutomationElement.NativeWindowHandle.TryGetValue(out var hWnd))
                    {
                        PInvoke.SetForegroundWindow((HWND)hWnd);
                    }

                    if (element.Properties.IsKeyboardFocusable)
                    {
                        element.Focus();
                        Retry.WhileFalse(() => element.Properties.HasKeyboardFocus, TimeSpan.FromSeconds(0.5));
                    }

                    SendUnicodeString(value ?? string.Empty);
                    return true;
                }
            }
        }

        public PixelRect BoundingRectangle => new(
            element.BoundingRectangle.X,
            element.BoundingRectangle.Y,
            element.BoundingRectangle.Width,
            element.BoundingRectangle.Height);

        public uint ProcessId => (uint)element.FrameworkAutomationElement.ProcessId.ValueOrDefault;

        public override string ToString() =>
            $"[{element.ControlType}] {(string.IsNullOrWhiteSpace(Text) ? "(Empty)" : Text)} - {element.Properties.FullDescription} - {element.Properties.HelpText}";

        private unsafe static void SendUnicodeString(string text)
        {
            var inputs = new Span<INPUT>(new INPUT[text.Length * 2]);

            for (var i = 0; i < text.Length; i++)
            {
                ref var inputDown = ref inputs[i];
                ref var inputUp = ref inputs[i + 1];

                inputDown.type = inputUp.type = INPUT_TYPE.INPUT_KEYBOARD;
                inputDown.Anonymous.ki.wScan = inputUp.Anonymous.ki.wScan = text[i];
                inputDown.Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE;
                inputUp.Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;
            }

            var inputBlocked = PInvoke.BlockInput(true);
            PInvoke.SendInput(inputs, sizeof(INPUT));
            if (inputBlocked) PInvoke.BlockInput(false);
        }
    }
}