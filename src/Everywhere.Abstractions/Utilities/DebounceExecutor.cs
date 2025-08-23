namespace Everywhere.Utilities;

/// <summary>
/// A high-performance, low-allocation debounced executor.
/// It debounces calls to a parameterless method and, when the delay has passed,
/// it invokes a value provider (Func{T}) and passes the result to an action (Action{T}).
/// </summary>
/// <typeparam name="T">The type of the value to be processed.</typeparam>
public sealed class DebounceExecutor<T> : IDisposable
{
    private readonly Timer _timer;
    private readonly Func<T> _valueProvider;
    private readonly Action<T> _action;
    private readonly TimeSpan _delay;

    private volatile bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DebounceExecutor{T}"/> class.
    /// </summary>
    /// <param name="valueProvider">The function to call to get the value when the action is to be executed.</param>
    /// <param name="action">The action to execute with the value from the provider.</param>
    /// <param name="delay">The debounce delay time.</param>
    public DebounceExecutor(Func<T> valueProvider, Action<T> action, TimeSpan delay)
    {
        _valueProvider = valueProvider;
        _action = action;
        _delay = delay;
        // The state object is 'this' instance, passed to the callback.
        _timer = new Timer(TimerCallback, this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// Triggers the execution of the action after the debounce delay.
    /// If called again before the delay has passed, the timer is reset.
    /// I've renamed Execute to Trigger, as it's a more fitting name for a parameterless method that starts a process.
    /// </summary>
    public void Trigger()
    {
        if (_isDisposed)
        {
            return;
        }

        // This is thread-safe. It will reset the timer to the specified delay.
        _timer.Change(_delay, Timeout.InfiniteTimeSpan);
    }

    public void Cancel()
    {
        if (_isDisposed)
        {
            return;
        }

        // Cancel the timer by setting the due time to infinite.
        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    private static void TimerCallback(object? state)
    {
        // The callback runs on a ThreadPool thread.
        var instance = (DebounceExecutor<T>)state!;
        if (instance._isDisposed)
        {
            return;
        }

        try
        {
            // Get the value and execute the action.
            var value = instance._valueProvider();
            instance._action(value);
        }
        catch
        {
            // Depending on requirements, you might want to log exceptions here.
            // By default, we suppress exceptions from the provider or action to prevent the timer from crashing.
        }
    }

    /// <summary>
    /// Disposes the executor, stopping any pending operations.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;

        // Dispose the timer, which prevents any further callbacks.
        // The WaitHandle ensures that we wait for any currently executing callback to complete.
        using (var waitHandle = new ManualResetEvent(false))
        {
            if (_timer.Dispose(waitHandle))
            {
                waitHandle.WaitOne();
            }
        }
    }
}