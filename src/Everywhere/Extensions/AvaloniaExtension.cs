using Avalonia.Controls;
using Avalonia.Threading;
using Everywhere.Models;

namespace Everywhere.Extensions;

public static class AvaloniaExtension
{
    public static void InvokeOnDemand(this Dispatcher dispatcher, Action action)
    {
        if (dispatcher.CheckAccess()) action();
        else dispatcher.Invoke(action);
    }

    public static void InvokeOnDemand(this Dispatcher dispatcher, Action action, in DispatcherPriority priority)
    {
        if (dispatcher.CheckAccess()) action();
        else dispatcher.Invoke(action, priority);
    }

    public static void InvokeOnDemand(
        this Dispatcher dispatcher,
        Action action,
        in DispatcherPriority priority,
        in CancellationToken cancellationToken)
    {
        if (dispatcher.CheckAccess()) action();
        else dispatcher.Invoke(action, priority, cancellationToken);
    }

    public static void InvokeOnDemand(
        this Dispatcher dispatcher,
        Action action,
        in DispatcherPriority priority,
        in CancellationToken cancellationToken,
        in TimeSpan timeout)
    {
        if (dispatcher.CheckAccess()) action();
        else dispatcher.Invoke(action, priority, cancellationToken, timeout);
    }

    public static T InvokeOnDemand<T>(this Dispatcher dispatcher, Func<T> func)
    {
        return dispatcher.CheckAccess() ? func() : dispatcher.Invoke(func);
    }

    public static T InvokeOnDemand<T>(
        this Dispatcher dispatcher,
        Func<T> func,
        in DispatcherPriority priority)
    {
        return dispatcher.CheckAccess() ? func() : dispatcher.Invoke(func, priority);
    }

    public static T InvokeOnDemand<T>(
        this Dispatcher dispatcher,
        Func<T> func,
        in DispatcherPriority priority,
        in CancellationToken cancellationToken)
    {
        return dispatcher.CheckAccess() ? func() : dispatcher.Invoke(func, priority, cancellationToken);
    }

    public static T InvokeOnDemand<T>(
        this Dispatcher dispatcher,
        Func<T> func,
        in DispatcherPriority priority,
        in CancellationToken cancellationToken,
        in TimeSpan timeout)
    {
        return dispatcher.CheckAccess() ? func() : dispatcher.Invoke(func, priority, cancellationToken, timeout);
    }

    public static Task InvokeOnDemandAsync(this Dispatcher dispatcher, Action action)
    {
        if (!dispatcher.CheckAccess()) return dispatcher.InvokeAsync(action).GetTask();
        action();
        return Task.CompletedTask;
    }

    public static Task InvokeOnDemandAsync(
        this Dispatcher dispatcher,
        Action action,
        in DispatcherPriority priority)
    {
        if (!dispatcher.CheckAccess()) return dispatcher.InvokeAsync(action, priority).GetTask();
        action();
        return Task.CompletedTask;
    }

    public static Task InvokeOnDemandAsync(
        this Dispatcher dispatcher,
        Action action,
        in DispatcherPriority priority,
        in CancellationToken cancellationToken)
    {
        if (!dispatcher.CheckAccess()) return dispatcher.InvokeAsync(action, priority, cancellationToken).GetTask();
        action();
        return Task.CompletedTask;
    }

    public static Task<T> InvokeOnDemandAsync<T>(this Dispatcher dispatcher, Func<T> func)
    {
        return dispatcher.CheckAccess() ? Task.FromResult(func()) : dispatcher.InvokeAsync(func).GetTask();
    }

    public static Task<T> InvokeOnDemandAsync<T>(
        this Dispatcher dispatcher,
        Func<T> func,
        in DispatcherPriority priority)
    {
        return dispatcher.CheckAccess() ? Task.FromResult(func()) : dispatcher.InvokeAsync(func, priority).GetTask();
    }

    public static Task<T> InvokeOnDemandAsync<T>(
        this Dispatcher dispatcher,
        Func<T> func,
        in DispatcherPriority priority,
        in CancellationToken cancellationToken)
    {
        return dispatcher.CheckAccess() ? Task.FromResult(func()) : dispatcher.InvokeAsync(func, priority, cancellationToken).GetTask();
    }

    /// <summary>
    /// Run and wait for a Task on the DispatcherFrame, allowing the UI thread to remain responsive
    /// </summary>
    /// <param name="task"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="AggregateException"></exception>
    public static void WaitOnDispatcherFrame(this Task task, CancellationToken cancellationToken = default)
    {
        var frame = new DispatcherFrame();
        AggregateException? capturedException = null;

        if (cancellationToken != CancellationToken.None)
        {
            cancellationToken.Register(() => frame.Continue = false);
        }
        task.ContinueWith(
            t =>
            {
                capturedException = t.Exception;
                frame.Continue = false; // 结束消息循环
            },
            TaskContinuationOptions.AttachedToParent);

        Dispatcher.UIThread.PushFrame(frame);

        if (capturedException != null)
        {
            throw capturedException;
        }
    }

    /// <summary>
    /// Run and wait for a Task on the DispatcherFrame, allowing the UI thread to remain responsive
    /// </summary>
    /// <param name="task"></param>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    /// <exception cref="AggregateException"></exception>
    public static TResult WaitOnDispatcherFrame<TResult>(this Task<TResult> task)
    {
        var frame = new DispatcherFrame();

        TResult? result = default;

        AggregateException? capturedException = null;

        task.ContinueWith(
            t =>
            {
                capturedException = t.Exception;
                result = t.Result;
                frame.Continue = false; // 结束消息循环
            },
            TaskContinuationOptions.AttachedToParent);

        Dispatcher.UIThread.PushFrame(frame);

        if (capturedException != null)
        {
            throw capturedException;
        }

        return result ?? throw new InvalidOperationException("Task result is null");
    }

    public static TextBlock ToTextBlock(this DynamicResourceKey dynamicResourceKey)
    {
        return new TextBlock
        {
            Classes = { nameof(DynamicResourceKey) },
            [!TextBlock.TextProperty] = dynamicResourceKey.ToBinding()
        };
    }
}