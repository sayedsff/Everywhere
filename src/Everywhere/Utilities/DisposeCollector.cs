using System.Diagnostics.CodeAnalysis;

namespace Everywhere.Utilities;

public class DisposeCollector<T>(bool enableDisposeOnFinalize = false) : IReadOnlyList<T>, IDisposable where T : IDisposable
{
    public bool IsDisposed { get; private set; }

    public bool EnableDisposeOnFinalize { get; set; } = enableDisposeOnFinalize;

    protected readonly List<T> _disposables = [];

    ~DisposeCollector()
    {
        if (EnableDisposeOnFinalize) Dispose();
    }

    public T Add(T disposable)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(DisposeCollector<>));
        _disposables.Add(disposable);
        return disposable;
    }

    public T Add(Func<T> factory)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(DisposeCollector<>));
        var disposable = factory();
        _disposables.Add(disposable);
        return disposable;
    }

    public void RemoveAndDispose(ref T? disposable)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(DisposeCollector<>));
        if (disposable == null) return;
        disposable.Dispose();
        if (!_disposables.Remove(disposable)) return;
        disposable = default;
    }

    /// <summary>
    /// 清空后还能继续使用
    /// </summary>
    public void DisposeAndClear()
    {
        // 逆序释放，防止释放后的对象被再次使用
        foreach (var disposable in _disposables.Reversed()) disposable.Dispose();
        _disposables.Clear();
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        $"{GetType().Name} is disposing".DebugWriteLineWithDateTime();
        IsDisposed = true;
        DisposeAndClear();
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

public class DisposeCollector(bool enableDisposeOnFinalize = false) : DisposeCollector<IDisposable>(enableDisposeOnFinalize)
{
    public void Add(Action disposer) => Add(new AnonymousDisposable(disposer));
    
    public T Add<T>(T disposable) where T : IDisposable
    {
        base.Add(disposable);
        return disposable;
    }

    public void RemoveAndDispose<T>(ref T? disposable) where T : IDisposable
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(DisposeCollector<>));
        if (disposable == null) return;
        _disposables.Remove(disposable);
        disposable.Dispose();
        disposable = default;
    }

    public void Replace<T>([NotNullIfNotNull(nameof(newDisposable))] ref T? oldDisposable, T? newDisposable) where T : IDisposable
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(DisposeCollector<>));
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