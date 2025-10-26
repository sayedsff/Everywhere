using System.Diagnostics.CodeAnalysis;

namespace Everywhere.Utilities;

/// <summary>
/// A collector for IDisposable objects that can dispose them all at once.
/// This also provides static utilities for disposing IDisposable objects to default.
/// </summary>
/// <param name="disposeOnFinalize"></param>
/// <typeparam name="T"></typeparam>
public class DisposeCollector<T>(bool disposeOnFinalize = false) : IReadOnlyList<T>, IDisposable where T : IDisposable
{
    /// <summary>
    /// Indicates whether the collector has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Indicates whether to dispose the collected disposables when the collector is finalized.
    /// </summary>
    public bool DisposeOnFinalize { get; set; } = disposeOnFinalize;

    protected readonly List<T> _disposables = [];

    ~DisposeCollector()
    {
        if (DisposeOnFinalize) Dispose();
    }

    public T Add(T disposable)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, nameof(DisposeCollector<>));
        _disposables.Add(disposable);
        return disposable;
    }

    public T Add(Func<T> factory)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, nameof(DisposeCollector<>));
        var disposable = factory();
        _disposables.Add(disposable);
        return disposable;
    }

    public void RemoveAndDispose(ref T? disposable)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, nameof(DisposeCollector<>));
        if (disposable == null) return;
        disposable.Dispose();
        if (!_disposables.Remove(disposable)) return;
        disposable = default;
    }

    /// <summary>
    /// Clear the collector and dispose all collected disposables. You can continue to use the collector after calling this method.
    /// </summary>
    public void Clear()
    {
        // Dispose in reverse order to prevent disposed objects from being used again
        foreach (var disposable in _disposables.Reversed()) disposable.Dispose();
        _disposables.Clear();
    }

    /// <summary>
    /// Dispose the collector and all collected disposables. After calling this method, the collector cannot be used anymore.
    /// </summary>
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        Clear();
        GC.SuppressFinalize(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DisposeToDefault(ref T? disposable)
    {
        if (disposable is null) return;
        disposable.Dispose();
        disposable = default;
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _disposables.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)_disposables).GetEnumerator();
    }

    public int Count => _disposables.Count;
    public T this[int index] => _disposables[index];
}

public class DisposeCollector(bool disposeOnFinalize = false) : DisposeCollector<IDisposable>(disposeOnFinalize)
{
    public void Add(Action disposer) => Add(new AnonymousDisposable(disposer));
    
    public T Add<T>(T disposable) where T : IDisposable
    {
        base.Add(disposable);
        return disposable;
    }

    public void RemoveAndDispose<T>(ref T? disposable) where T : IDisposable
    {
        ObjectDisposedException.ThrowIf(IsDisposed, nameof(DisposeCollector<>));
        if (disposable == null) return;
        _disposables.Remove(disposable);
        disposable.Dispose();
        disposable = default;
    }

    public void Replace<T>([NotNullIfNotNull(nameof(newDisposable))] ref T? oldDisposable, T? newDisposable) where T : IDisposable
    {
        ObjectDisposedException.ThrowIf(IsDisposed, nameof(DisposeCollector<>));
        if (oldDisposable != null)
        {
            oldDisposable.Dispose();
            _disposables.Remove(oldDisposable);
        }
        
        oldDisposable = newDisposable;
        if (oldDisposable == null) return;
        _disposables.Add(oldDisposable);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DisposeToDefault<T>(ref T? disposable) where T : IDisposable
    {
        if (disposable is null) return;
        disposable.Dispose();
        disposable = default;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FreeHGlobalToNull(ref IntPtr ptr)
    {
        Marshal.FreeHGlobal(ptr);
        ptr = IntPtr.Zero;
    }
}