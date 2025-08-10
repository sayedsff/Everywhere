namespace Everywhere.Utilities;

/// <summary>
/// A CancellationTokenSource that can be reused after being cancelled.
/// Thread-safe.
/// </summary>
public class ReusableCancellationTokenSource
{
    private readonly Lock _lockObject = new();
    private CancellationTokenSource? _cancellationTokenSource;

    public CancellationToken Token
    {
        get
        {
            using var _ = _lockObject.EnterScope();
            _cancellationTokenSource ??= new CancellationTokenSource();
            return _cancellationTokenSource.Token;
        }
    }

    public void Cancel()
    {
        using var _ = _lockObject.EnterScope();
        if (_cancellationTokenSource == null) return;
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = null;
    }
}