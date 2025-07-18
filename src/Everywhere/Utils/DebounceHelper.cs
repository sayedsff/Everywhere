namespace Everywhere.Utils;

/// <summary>
/// Efficient debounce helper
/// </summary>
/// <param name="time">Debounce delay time</param>
public sealed class DebounceHelper(TimeSpan time) : IDisposable
{
    private readonly Lock lockObj = new();
    private volatile CancellationTokenSource? cancellationTokenSource;
    private volatile bool isDisposed;
    private volatile Task? debounceTask;

    /// <summary>
    /// Attempts to execute an operation. If called again within the specified time, cancels the previous operation
    /// </summary>
    /// <param name="action">The operation to execute</param>
    /// <returns>Returns true if the operation is scheduled for execution, false if the object is disposed</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Execute(Action action)
    {
        if (isDisposed) return;

        ArgumentNullException.ThrowIfNull(action);

        lock (lockObj)
        {
            if (isDisposed) return;

            // Cancel the previous operation
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();

            // Create a new cancellation token
            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;

            // Use Task.Delay for efficient asynchronous delay
            debounceTask = Task.Delay(time, token).ContinueWith(
                static (task, state) =>
                {
                    if (!task.IsCanceled && state is Action actionToExecute)
                    {
                        actionToExecute();
                    }
                },
                action,
                token,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    /// <summary>
    /// Attempts to execute an operation. Returns false if currently executing or if less than <see cref="time"/> has passed since the last execution
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <returns>True if the action was scheduled, false otherwise</returns>
    public bool TryExecute(Action action)
    {
        if (isDisposed) return false;

        ArgumentNullException.ThrowIfNull(action);

        lock (lockObj)
        {
            if (isDisposed) return false;

            // If currently executing or less than time has passed since the last execution, return false
            if (debounceTask is not null && !debounceTask.IsCompleted)
            {
                return false;
            }

            // Execute the operation
            Execute(action);
            return true;
        }
    }

    public void Dispose()
    {
        if (isDisposed) return;

        lock (lockObj)
        {
            if (isDisposed) return;

            isDisposed = true;
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
        }
    }
}