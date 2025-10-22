using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using Avalonia.Input;
using Everywhere.Interop;
using Everywhere.Windows.Extensions;

namespace Everywhere.Windows.Interop;

public unsafe class Win32HotkeyListener : IHotkeyListener
{
    private static HWND _hwnd;

    // Unique id generator for RegisterHotKey
    private static int _nextId = 1;

    // OS-registered keyboard hotkeys: hotkey -> registration bundle (id + handlers)
    private static readonly Dictionary<KeyboardHotkey, OsReg> OsRegs = new();
    private static readonly Dictionary<int, OsReg> IdToOsReg = new();

    // Fallback keyboard hotkeys (when RegisterHotKey fails): (mods,key) -> handlers
    private static readonly Dictionary<HotkeySig, HandlerList> HookKbHandlers = new();

    // Mouse hotkeys: per-button registrations (each has its own delay/handler/timer)
    private static readonly Dictionary<MouseButton, List<MouseRegistration>> MouseRegs = new()
    {
        { MouseButton.Left, [] },
        { MouseButton.Right, [] },
        { MouseButton.Middle, [] },
        { MouseButton.XButton1, [] },
        { MouseButton.XButton2, [] },
    };

    // Hooks (created on demand)
    private static LowLevelKeyboardHook? _keyboardHook;
    private static LowLevelMouseHook? _mouseHook;

    // Inject sentinel to ignore self-injected events
    private const nuint InjectExtra = 0x0d000721;

    // Keyboard hook state
    private static KeyModifiers _pressedMods;
    private static bool _triggerActive;
    private static HotkeySig _activeSig;
    private static bool _winHeldSuppressed;
    private static bool _injectedWinDown;
    private static VIRTUAL_KEY _winVk;

    // Concurrency: light lock for hotkey maps
    private static readonly Lock SyncLock = new();

    private static IKeyboardHotkeyScope? _currentKeyboardHotkeyScope;

    static Win32HotkeyListener()
    {
        var t = new Thread(WindowLoop) { IsBackground = true, Name = "HotkeyWindow", };
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
    }

    public IKeyboardHotkeyScope StartCaptureKeyboardHotkey()
    {
        lock (SyncLock)
        {
            if (_currentKeyboardHotkeyScope is not { IsDisposed: false }) _currentKeyboardHotkeyScope = new KeyboardHotkeyScopeImpl();
            return _currentKeyboardHotkeyScope;
        }
    }

    public IDisposable Register(KeyboardHotkey hotkey, Action handler)
    {
        if (hotkey.Key == Key.None || hotkey.Modifiers == KeyModifiers.None)
            throw new ArgumentException("Invalid keyboard hotkey.", nameof(hotkey));
        if (handler is null) throw new ArgumentNullException(nameof(handler));

        lock (SyncLock)
        {
            // If there is already an OS registration for this hotkey, reuse id and append handler.
            if (OsRegs.TryGetValue(hotkey, out var reg))
            {
                reg.Handlers.Add(handler);
                return new Disposer(() => UnregisterKeyboardHandlerOs(hotkey, handler));
            }

            // Try OS registration (incl. MOD_WIN).
            var mods = HOT_KEY_MODIFIERS.MOD_NOREPEAT;
            if (hotkey.Modifiers.HasFlag(KeyModifiers.Control)) mods |= HOT_KEY_MODIFIERS.MOD_CONTROL;
            if (hotkey.Modifiers.HasFlag(KeyModifiers.Shift)) mods |= HOT_KEY_MODIFIERS.MOD_SHIFT;
            if (hotkey.Modifiers.HasFlag(KeyModifiers.Alt)) mods |= HOT_KEY_MODIFIERS.MOD_ALT;
            if (hotkey.Modifiers.HasFlag(KeyModifiers.Meta)) mods |= HOT_KEY_MODIFIERS.MOD_WIN;

            var id = _nextId++;
            if (PInvoke.RegisterHotKey(_hwnd, id, mods, (uint)hotkey.Key.ToVirtualKey()))
            {
                var bundle = new OsReg(id, new List<Action> { handler });
                OsRegs[hotkey] = bundle;
                IdToOsReg[id] = bundle;
                return new Disposer(() => UnregisterKeyboardHandlerOs(hotkey, handler));
            }

            // Fallback to LL keyboard hook with Win suppression/compensation.
            var sig = new HotkeySig(hotkey.Modifiers, hotkey.Key);
            if (!HookKbHandlers.TryGetValue(sig, out var list))
            {
                list = new HandlerList();
                HookKbHandlers[sig] = list;
            }
            list.Add(handler);

            EnsureKeyboardHook();
            return new Disposer(() => UnregisterKeyboardHandlerHook(sig, handler));
        }
    }

    public IDisposable Register(MouseHotkey hotkey, Action handler)
    {
        if (handler is null) throw new ArgumentNullException(nameof(handler));

        lock (SyncLock)
        {
            var reg = new MouseRegistration(hotkey, handler);
            MouseRegs[hotkey.Key].Add(reg);
            EnsureMouseHook();
            return new Disposer(() => UnregisterMouseHandler(reg));
        }
    }

    public void Dispose()
    {
        lock (SyncLock)
        {
            // Unregister all OS hotkeys
            foreach (var kv in OsRegs)
            {
                PInvoke.UnregisterHotKey(_hwnd, kv.Value.Id);
            }
            OsRegs.Clear();
            IdToOsReg.Clear();

            // Clear fallback handlers
            HookKbHandlers.Clear();

            // Clear mouse regs (and cancel timers)
            foreach (var list in MouseRegs.Values)
            {
                foreach (var r in list) r.CancelTimer();
                list.Clear();
            }

            // Dispose hooks
            _keyboardHook?.Dispose();
            _keyboardHook = null;
            _mouseHook?.Dispose();
            _mouseHook = null;
        }
    }

    // ---------- window & message loop ----------

    private static void WindowLoop()
    {
        using var hModule = PInvoke.GetModuleHandle(null);
        _hwnd = PInvoke.CreateWindowEx(
            WINDOW_EX_STYLE.WS_EX_NOACTIVATE,
            "STATIC",
            "Everywhere.MultiHotkeyWindow",
            WINDOW_STYLE.WS_POPUP,
            0, 0, 0, 0,
            HWND.Null,
            null,
            hModule,
            null);

        if (_hwnd.IsNull)
            Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());

        MSG msg;
        while (PInvoke.GetMessage(&msg, HWND.Null, 0, 0) != 0)
        {
            switch (msg.message)
            {
                case (uint)WINDOW_MESSAGE.WM_HOTKEY:
                {
                    var id = (int)msg.wParam.Value;
                    OsReg? bundle;
                    lock (SyncLock)
                    {
                        IdToOsReg.TryGetValue(id, out bundle);
                    }
                    if (bundle is not null)
                    {
                        // Invoke OS-registered handlers
                        InvokeHandlers(bundle.Handlers);
                    }
                    break;
                }
            }

            PInvoke.DispatchMessage(&msg);
        }
    }

    private static void InvokeHandlers(List<Action> handlers)
    {
        // Execute handlers safely; avoid holding locks while invoking.
        foreach (var handler in handlers.ToArray())
        {
            try { handler(); } catch { /* swallow */ }
        }
    }

    // ---------- keyboard hook (fallback) ----------

    private static void EnsureKeyboardHook()
    {
        if (_keyboardHook is not null) return;
        _keyboardHook = new LowLevelKeyboardHook(KeyboardHookProc);
    }

    private static void KeyboardHookProc(UIntPtr wParam, ref KBDLLHOOKSTRUCT lParam, ref bool blockNext)
    {
        if (lParam.dwExtraInfo == InjectExtra) return;

        var msg = (WINDOW_MESSAGE)wParam;
        var vk = (VIRTUAL_KEY)lParam.vkCode;
        var key = vk.ToAvaloniaKey();
        var mod = vk.ToKeyModifiers();

        static bool IsWin(VIRTUAL_KEY v) => v is VIRTUAL_KEY.VK_LWIN or VIRTUAL_KEY.VK_RWIN;

        switch (msg)
        {
            case WINDOW_MESSAGE.WM_KEYDOWN:
            case WINDOW_MESSAGE.WM_SYSKEYDOWN:
            {
                if (mod != KeyModifiers.None)
                    _pressedMods |= mod;

                if (IsWin(vk) && AnyHookMetaHotkey())
                {
                    _winHeldSuppressed = true;
                    _injectedWinDown = false;
                    _winVk = vk;
                    blockNext = true;
                    return;
                }

                if (mod == KeyModifiers.None)
                {
                    var sig = new HotkeySig(_pressedMods, key);
                    List<Action>? handlers;
                    lock (SyncLock)
                    {
                        handlers = HookKbHandlers.TryGetValue(sig, out var list) ? list.Snapshot() : null;
                    }

                    if (!_triggerActive && handlers is not null)
                    {
                        _triggerActive = true;
                        _activeSig = sig;
                        blockNext = true; // block main key down
                        foreach (var h in handlers)
                        {
                            try { h(); } catch { /* swallow */ }
                        }
                        return;
                    }

                    if (_winHeldSuppressed)
                    {
                        // Not our hotkey: compensate Win down then let OS see this key
                        if (!_injectedWinDown)
                        {
                            InjectKey(_winVk, true);
                            _injectedWinDown = true;
                            _winHeldSuppressed = false;
                        }
                        return;
                    }
                }

                return;
            }

            case WINDOW_MESSAGE.WM_KEYUP:
            case WINDOW_MESSAGE.WM_SYSKEYUP:
            {
                if (mod != KeyModifiers.None)
                    _pressedMods &= ~mod;

                if (_triggerActive && key == _activeSig.Key)
                {
                    blockNext = true; // block main key up
                }

                if (IsWin(vk) && AnyHookMetaHotkey())
                {
                    if (_triggerActive && _activeSig.Modifiers.HasFlag(KeyModifiers.Meta))
                    {
                        // Our combo contained Meta: swallow Win up to avoid Start menu
                        blockNext = true;
                    }
                    else if (_injectedWinDown)
                    {
                        // We injected Win down: swallow real up, inject up
                        blockNext = true;
                        InjectKey(_winVk, false);
                    }
                    else if (_winHeldSuppressed)
                    {
                        // Single-tap Win: swallow real up, inject down+up to open Start
                        blockNext = true;
                        InjectKey(_winVk, true);
                        InjectKey(_winVk, false);
                    }

                    _winHeldSuppressed = false;
                    _injectedWinDown = false;
                    _winVk = 0;
                }

                if (_pressedMods == KeyModifiers.None)
                {
                    _triggerActive = false;
                    _activeSig = default;
                }

                return;
            }
        }
    }

    private static bool AnyHookMetaHotkey()
    {
        lock (SyncLock)
        {
            foreach (var k in HookKbHandlers.Keys)
                if ((k.Modifiers & KeyModifiers.Meta) != 0) return true;
        }
        return false;
    }

    private static void InjectKey(VIRTUAL_KEY vk, bool down)
    {
        var inputs = new INPUT[1];
        inputs[0] = new INPUT
        {
            type = INPUT_TYPE.INPUT_KEYBOARD,
            Anonymous = new INPUT._Anonymous_e__Union
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = 0,
                    dwFlags = down ? 0 : KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = InjectExtra
                }
            }
        };
        fixed (INPUT* p = inputs)
        {
            PInvoke.SendInput(new ReadOnlySpan<INPUT>(p, 1), sizeof(INPUT));
        }
    }

    // ---------- mouse hook ----------

    private static void EnsureMouseHook()
    {
        if (_mouseHook is not null) return;
        _mouseHook = new LowLevelMouseHook(MouseHookProc);
    }

    private static void MouseHookProc(nuint wParam, ref MSLLHOOKSTRUCT hs, ref bool blockNext)
    {
        if (hs.dwExtraInfo == InjectExtra) return;

        var msg = (WINDOW_MESSAGE)wParam;
        var button = (hs.mouseData >> 16) & 0xFFFF;

        switch (msg)
        {
            case WINDOW_MESSAGE.WM_LBUTTONDOWN:
            case WINDOW_MESSAGE.WM_RBUTTONDOWN:
            case WINDOW_MESSAGE.WM_MBUTTONDOWN:
            case WINDOW_MESSAGE.WM_XBUTTONDOWN:
            {
                if (GetRegistrations() is not { Count: > 0 } registrations) break;

                // Schedule or fire per registration
                foreach (var r in registrations) r.OnDown();
                break;
            }

            case WINDOW_MESSAGE.WM_LBUTTONUP:
            case WINDOW_MESSAGE.WM_RBUTTONUP:
            case WINDOW_MESSAGE.WM_MBUTTONUP:
            case WINDOW_MESSAGE.WM_XBUTTONUP:
            {
                if (GetRegistrations() is not { Count: > 0 } registrations) break;

                // Cancel pending per registration
                foreach (var r in registrations) r.OnUp();
                break;
            }
        }

        List<MouseRegistration>? GetRegistrations()
        {
            var mk = msg switch
            {
                WINDOW_MESSAGE.WM_LBUTTONUP => MouseButton.Left,
                WINDOW_MESSAGE.WM_RBUTTONUP => MouseButton.Right,
                WINDOW_MESSAGE.WM_MBUTTONUP => MouseButton.Middle,
                WINDOW_MESSAGE.WM_XBUTTONUP when button == PInvoke.XBUTTON1 => MouseButton.XButton1,
                WINDOW_MESSAGE.WM_XBUTTONUP when button == PInvoke.XBUTTON2 => MouseButton.XButton2,
                _ => MouseButton.None
            };
            if (mk == MouseButton.None) return null;

            lock (SyncLock) return MouseRegs[mk].Count > 0 ? [..MouseRegs[mk]] : null;
        }
    }

    // ---------- unregister helpers ----------

    private static void UnregisterKeyboardHandlerOs(KeyboardHotkey hotkey, Action handler)
    {
        lock (SyncLock)
        {
            if (!OsRegs.TryGetValue(hotkey, out var bundle)) return;
            bundle.Handlers.Remove(handler);
            if (bundle.Handlers.Count > 0) return;

            OsRegs.Remove(hotkey);
            IdToOsReg.Remove(bundle.Id);
            PInvoke.UnregisterHotKey(_hwnd, bundle.Id);
        }
    }

    private static void UnregisterKeyboardHandlerHook(HotkeySig sig, Action handler)
    {
        lock (SyncLock)
        {
            if (!HookKbHandlers.TryGetValue(sig, out var list)) return;
            list.Remove(handler);
            if (list.Count == 0) HookKbHandlers.Remove(sig);
            if (HookKbHandlers.Count == 0 && _keyboardHook is not null)
            {
                _keyboardHook.Dispose();
                _keyboardHook = null;
            }
        }
    }

    private static void UnregisterMouseHandler(MouseRegistration reg)
    {
        lock (SyncLock)
        {
            if (MouseRegs.TryGetValue(reg.Hotkey.Key, out var list))
            {
                list.Remove(reg);
                reg.CancelTimer();
            }
            if (MouseRegs.Values.Sum(l => l.Count) == 0 && _mouseHook is not null)
            {
                _mouseHook.Dispose();
                _mouseHook = null;
            }
        }
    }

    // ---------- small types ----------

    private readonly record struct Disposer(Action DisposeAction) : IDisposable
    {
        public void Dispose() => DisposeAction?.Invoke();
    }

    private sealed class OsReg(int id, List<Action> handlers)
    {
        public int Id { get; } = id;
        public List<Action> Handlers { get; } = handlers;
    }

    private readonly struct HotkeySig(KeyModifiers modifiers, Key key) : IEquatable<HotkeySig>
    {
        public KeyModifiers Modifiers { get; } = modifiers;
        public Key Key { get; } = key;
        public bool Equals(HotkeySig other) => Modifiers == other.Modifiers && Key == other.Key;
        public override bool Equals(object? obj) => obj is HotkeySig o && Equals(o);
        public override int GetHashCode() => ((int)Modifiers << 16) ^ (int)Key;
    }

    private sealed class HandlerList
    {
        private readonly List<Action> _handlers = [];
        public void Add(Action h) => _handlers.Add(h);
        public void Remove(Action h) => _handlers.Remove(h);
        public int Count => _handlers.Count;
        public List<Action> Snapshot() => [.._handlers];
    }

    // Per-registration mouse state (timer lifecycle)
    private sealed class MouseRegistration(MouseHotkey hotkey, Action handler)
    {
        public MouseHotkey Hotkey { get; } = hotkey;
        public Action Handler { get; } = handler;
        private Timer? _timer;
        private int _armed;    // 1 when button is considered pressed/pending

        public void OnDown()
        {
            // mark pressed
            Interlocked.Exchange(ref _armed, 1);

            if (Hotkey.Delay <= TimeSpan.Zero)
            {
                SafeInvoke();
                return;
            }

            CancelTimer(); // defensive
            _timer = new Timer(_ =>
            {
                if (Interlocked.CompareExchange(ref _armed, 1, 1) == 1)
                {
                    SafeInvoke();
                }
            }, null, Hotkey.Delay, Timeout.InfiniteTimeSpan);
        }

        public void OnUp()
        {
            Interlocked.Exchange(ref _armed, 0);
            CancelTimer();
        }

        public void CancelTimer()
        {
            var t = Interlocked.Exchange(ref _timer, null);
            t?.Dispose();
        }

        private void SafeInvoke()
        {
            try { Handler(); } catch { /* swallow */ }
        }
    }

    private sealed class KeyboardHotkeyScopeImpl : IKeyboardHotkeyScope
    {
        public KeyboardHotkey PressingHotkey { get; private set; }

        public bool IsDisposed { get; private set; }

        public event IKeyboardHotkeyScope.PressingHotkeyChangedHandler? PressingHotkeyChanged;

        public event IKeyboardHotkeyScope.HotkeyFinishedHandler? HotkeyFinished;

        private readonly LowLevelKeyboardHook _keyboardHook;

        private KeyModifiers _pressedKeyModifiers = KeyModifiers.None;

        public KeyboardHotkeyScopeImpl()
        {
            _keyboardHook = new LowLevelKeyboardHook(KeyboardHookCallback);
        }

        private void KeyboardHookCallback(UIntPtr wParam, ref KBDLLHOOKSTRUCT lParam, ref bool blockNext)
        {
            var virtualKey = (VIRTUAL_KEY)lParam.vkCode;
            var keyModifiers = virtualKey.ToKeyModifiers();

            switch ((WINDOW_MESSAGE)wParam)
            {
                case WINDOW_MESSAGE.WM_KEYDOWN or WINDOW_MESSAGE.WM_SYSKEYDOWN when keyModifiers == KeyModifiers.None:
                {
                    PressingHotkey = PressingHotkey with { Key = virtualKey.ToAvaloniaKey() };
                    PressingHotkeyChanged?.Invoke(this, PressingHotkey);
                    break;
                }
                case WINDOW_MESSAGE.WM_KEYDOWN or WINDOW_MESSAGE.WM_SYSKEYDOWN:
                {
                    _pressedKeyModifiers |= keyModifiers;
                    PressingHotkey = PressingHotkey with { Modifiers = _pressedKeyModifiers };
                    PressingHotkeyChanged?.Invoke(this, PressingHotkey);
                    break;
                }
                case WINDOW_MESSAGE.WM_KEYUP or WINDOW_MESSAGE.WM_SYSKEYUP:
                {
                    _pressedKeyModifiers &= ~keyModifiers;
                    if (_pressedKeyModifiers == KeyModifiers.None)
                    {
                        if (PressingHotkey.Modifiers != KeyModifiers.None && PressingHotkey.Key == Key.None)
                        {
                            PressingHotkey = default; // modifiers only hotkey, reset it
                        }

                        // system key is all released, capture is done
                        PressingHotkeyChanged?.Invoke(this, PressingHotkey);
                        HotkeyFinished?.Invoke(this, PressingHotkey);
                    }
                    break;
                }
            }

            blockNext = true;
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;

            _keyboardHook.Dispose();
        }
    }
}