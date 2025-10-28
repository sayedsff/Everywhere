using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Everywhere.Extensions;
using Everywhere.Utilities;

namespace Everywhere.Windows.Interop;

// Shared message-only window host on a dedicated STA thread.
// Consumers can add message handlers and reuse HWND for OS APIs (e.g., RegisterHotKey, AddClipboardFormatListener).
internal sealed class Win32MessageWindow
{
    public static Win32MessageWindow Shared { get; } = new();

    public HWND HWnd { get; private set; }

    public delegate void MessageHandler(in MSG msg);

    private readonly Lock _lock = new();
    private readonly Dictionary<uint, List<MessageHandler>> _handlers = new();

    private Win32MessageWindow()
    {
        var thread = new Thread(WindowLoop)
        {
            IsBackground = true,
            Name = "Everywhere.MessageWindow",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public IDisposable AddHandler(uint message, MessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_lock)
        {
            if (!_handlers.TryGetValue(message, out var list))
            {
                list = [];
                _handlers[message] = list;
            }
            list.Add(handler);
        }
        return new AnonymousDisposable(() => RemoveHandler(message, handler));
    }

    private void RemoveHandler(uint message, MessageHandler handler)
    {
        lock (_lock)
        {
            if (!_handlers.TryGetValue(message, out var list)) return;
            list.Remove(handler);
            if (list.Count == 0) _handlers.Remove(message);
        }
    }

    private unsafe void WindowLoop()
    {
        using var hModule = PInvoke.GetModuleHandle(null);

        // Create a message-only window (child of HWND_MESSAGE)
        HWnd = PInvoke.CreateWindowEx(
            0,
            "STATIC",
            "Everywhere.MessageWindow",
            0,
            0, 0, 0, 0,
            new HWND(-3), // HWND_MESSAGE
            null,
            hModule,
            null);

        if (HWnd.IsNull)
            throw new InvalidOperationException("Failed to create message window.");


        MSG msg;
        while (PInvoke.GetMessage(&msg, HWND.Null, 0, 0) != 0)
        {
            // Dispatch to registered handlers first
            List<MessageHandler> snapshot = [];
            lock (_lock)
            {
                if (_handlers.TryGetValue(msg.message, out var list) && list.Count > 0)
                    snapshot.Reset(list);
            }
            foreach (var h in snapshot)
            {
                try { h(in msg); } catch { /* swallow */ }
            }

            PInvoke.TranslateMessage(&msg);
            PInvoke.DispatchMessage(&msg);
        }
    }
}
