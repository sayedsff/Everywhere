using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Everywhere.Interfaces;
using Point = System.Drawing.Point;

namespace Everywhere.Windows.Services;

public partial class Win32VisualElementContext
{
    private class ElementPicker : Window
    {
        private readonly Win32VisualElementContext context;
        private readonly INativeHelper nativeHelper;
        private readonly PickElementMode mode;

        private readonly PixelRect screenBounds;
        private readonly Bitmap bitmap;
        private readonly Border clipBorder;
        private readonly Image image;
        private readonly double scale;
        private readonly TaskCompletionSource<IVisualElement?> taskCompletionSource = new();

        private Rect? previousMaskRect;
        private IVisualElement? selectedElement;

        private ElementPicker(
            Win32VisualElementContext context,
            INativeHelper nativeHelper,
            PickElementMode mode)
        {
            this.context = context;
            this.nativeHelper = nativeHelper;
            this.mode = mode;

            var allScreens = Screens.All;
            screenBounds = allScreens.Aggregate(default(PixelRect), (current, screen) => current.Union(screen.Bounds));
            if (screenBounds.Width <= 0 || screenBounds.Height <= 0)
            {
                throw new InvalidOperationException("No valid screen bounds found.");
            }

            bitmap = CaptureScreen(screenBounds);
            clipBorder = new Border
            {
                ClipToBounds = false,
                BorderThickness = new Thickness(2),
                BorderBrush = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            Content = new Panel
            {
                IsHitTestVisible = false,
                Children =
                {
                    new Image { Source = bitmap },
                    new Border
                    {
                        Background = Brushes.Black,
                        Opacity = 0.4
                    },
                    (image = new Image { Source = bitmap }),
                    clipBorder
                }
            };

            Topmost = true;
            CanResize = false;
            ShowInTaskbar = false;
            Cursor = new Cursor(StandardCursorType.Cross);
            SystemDecorations = SystemDecorations.None;
            WindowStartupLocation = WindowStartupLocation.Manual;

            Position = screenBounds.Position;
            scale = DesktopScaling; // we must set Position first to get the correct scaling factor
            Width = screenBounds.Width / scale;
            Height = screenBounds.Height / scale;
        }

        protected override unsafe void OnPointerEntered(PointerEventArgs e)
        {
            // SendInput MouseLeftButtonDown, this will:
            // 1. prevent the cursor from changing to the default arrow cursor and interacting with other windows
            // 2. Trigger the OnPointerPressed event to set the window to hit test invisible
            PInvoke.SendInput(
                new ReadOnlySpan<INPUT>(
                [
                    new INPUT
                    {
                        type = INPUT_TYPE.INPUT_MOUSE,
                        Anonymous = new INPUT._Anonymous_e__Union
                        {
                            mi = new MOUSEINPUT
                            {
                                dwFlags = MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN | MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE,
                                dx = Position.X + 1,
                                dy = Position.Y + 1
                            }
                        }
                    },
                ]),
                sizeof(INPUT));
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            nativeHelper.SetWindowHitTestInvisible(this);

            if (PInvoke.GetCursorPos(out var point)) Pick(point);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            if (PInvoke.GetCursorPos(out var point)) Pick(point);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            if (e.InitialPressMouseButton != MouseButton.Left)
            {
                selectedElement = null;
            }

            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape) selectedElement = null;

            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            bitmap.Dispose();
            taskCompletionSource.TrySetResult(selectedElement);
        }

        private void Pick(Point point)
        {
            var maskRect = new Rect();
            var pixelPoint = new PixelPoint(point.X, point.Y);
            switch (mode)
            {
                case PickElementMode.Screen:
                {
                    var screen = Screens.All.FirstOrDefault(s => s.Bounds.Contains(pixelPoint));
                    if (screen == null) break;

                    var hMonitor = PInvoke.MonitorFromPoint(point, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
                    if (hMonitor == HMONITOR.Null) break;

                    selectedElement = new ScreenVisualElementImpl(context, hMonitor);

                    maskRect = screen.Bounds.Translate(-(PixelVector)screenBounds.Position).ToRect(scale);
                    break;
                }
                case PickElementMode.Window:
                {
                    var selectedHWnd = PInvoke.WindowFromPoint(point);
                    if (selectedHWnd == HWND.Null) break;

                    var rootHWnd = PInvoke.GetAncestor(selectedHWnd, GET_ANCESTOR_FLAGS.GA_ROOTOWNER);
                    if (rootHWnd == HWND.Null) break;

                    selectedElement = context.TryFrom(() => Automation.FromHandle(rootHWnd));
                    if (selectedElement == null) break;

                    maskRect = selectedElement.BoundingRectangle.Translate(-(PixelVector)screenBounds.Position).ToRect(scale);
                    break;
                }
                case PickElementMode.Element:
                {
                    selectedElement = context.TryFrom(() => Automation.FromPoint(point));
                    if (selectedElement == null) break;

                    maskRect = selectedElement.BoundingRectangle.Translate(-(PixelVector)screenBounds.Position).ToRect(scale);
                    break;
                }
            }

            SetMask(maskRect);
        }

        private void SetMask(Rect rect)
        {
            if (previousMaskRect == rect) return;

            image.Clip = new RectangleGeometry(rect);
            clipBorder.Margin = new Thickness(rect.X, rect.Y, 0, 0);
            clipBorder.Width = rect.Width;
            clipBorder.Height = rect.Height;

            previousMaskRect = rect;
        }

        public static Task<IVisualElement?> PickAsync(
            Win32VisualElementContext context,
            INativeHelper nativeHelper,
            PickElementMode mode)
        {
            var window = new ElementPicker(context, nativeHelper, mode);
            window.Show();
            return window.taskCompletionSource.Task;
        }
    }
}