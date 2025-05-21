namespace Everywhere.Utils;

public class ReusableCancellationTokenSource
{
    private readonly Lock lockObject = new();
    private CancellationTokenSource? cancellationTokenSource;

    public CancellationToken Token
    {
        get
        {
            using var _ = lockObject.EnterScope();
            cancellationTokenSource ??= new CancellationTokenSource();
            return cancellationTokenSource.Token;
        }
    }

    public void Cancel()
    {
        using var _ = lockObject.EnterScope();
        if (cancellationTokenSource == null) return;
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
        cancellationTokenSource = null;
    }
}