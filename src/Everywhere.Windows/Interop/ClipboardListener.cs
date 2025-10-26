// Clipboard watcher based on the shared Win32MessageWindow.
// Call BeginWait() then WaitNextUpdate(timeoutMs) to await WM_CLIPBOARDUPDATE without polling.

using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Everywhere.Windows.Interop;

internal sealed class ClipboardListener
{
    public static ClipboardListener Shared { get; } = new();

    private readonly Lock _lock = new();
    private TaskCompletionSource<bool>? _tcs;
    private bool _subscribed;

    private ClipboardListener()
    {
        // Lazy subscribe to avoid AddClipboardFormatListener before HWND is created.
    }

    public void BeginWait()
    {
        EnsureSubscribed();
        lock (_lock)
        {
            _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public bool WaitNextUpdate(int timeoutMs)
    {
        TaskCompletionSource<bool>? tcs;
        lock (_lock) tcs = _tcs;
        if (tcs is null) return false;

        try
        {
            return tcs.Task.Wait(TimeSpan.FromMilliseconds(timeoutMs));
        }
        catch
        {
            return false;
        }
    }

    private void EnsureSubscribed()
    {
        if (_subscribed) return;

        var host = Win32MessageWindow.Shared;
        var hwnd = host.HWnd;

        // Register as clipboard format listener
        _ = PInvoke.AddClipboardFormatListener(hwnd);

        // Subscribe WM_CLIPBOARDUPDATE
        _ = host.AddHandler(PInvoke.WM_CLIPBOARDUPDATE, OnClipboardUpdate);

        _subscribed = true;
    }

    private void OnClipboardUpdate(in MSG _)
    {
        TaskCompletionSource<bool>? tcs;
        lock (_lock)
        {
            tcs = _tcs;
            _tcs = null;
        }
        tcs?.TrySetResult(true);
    }
}