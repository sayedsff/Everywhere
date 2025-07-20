using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using ShadUI;

namespace Everywhere.ViewModels;

public abstract class ReactiveViewModelBase : ObservableValidator
{
    protected internal DialogManager DialogManager { get; } = ServiceLocator.Resolve<DialogManager>();
    protected internal ToastManager ToastManager { get; } = ServiceLocator.Resolve<ToastManager>();

    protected void DialogExceptionHandler(Exception exception, string? message = null, [CallerMemberName] object? source = null) =>
        DialogManager.CreateDialog(message ?? "Error", exception.GetFriendlyMessage());

    protected void ToastExceptionHandler(Exception exception, string? message = null, [CallerMemberName] object? source = null) =>
        ToastManager.CreateToast(message ?? "Error")
            .WithContent(exception.GetFriendlyMessage())
            .DismissOnClick()
            .ShowError();

    protected internal virtual Task ViewLoaded(CancellationToken cancellationToken) => Task.CompletedTask;

    protected internal virtual Task ViewUnloaded() => Task.CompletedTask;

    protected virtual IExceptionHandler? LifetimeExceptionHandler => null;

    private void HandleLifetimeException(string stage, Exception e)
    {
        if (LifetimeExceptionHandler is not { } lifetimeExceptionHandler)
        {
            ToastManager.CreateToast($"Lifetime Exception: [{stage}]")
                .WithContent(e.GetFriendlyMessage())
                .DismissOnClick()
                .ShowError();
        }
        else
        {
            lifetimeExceptionHandler.HandleException(e, stage);
        }
    }

    public void Bind(Control target)
    {
        CancellationTokenSource? cancellationTokenSource = null;

        target.DataContext = this;
        target.Loaded += async (_, _) =>
        {
            try
            {
                cancellationTokenSource = new CancellationTokenSource();
                await ViewLoaded(cancellationTokenSource.Token);
            }
            catch (Exception e)
            {
                HandleLifetimeException(nameof(ViewLoaded), e);
            }
        };

        target.Unloaded += async (_, _) =>
        {
            try
            {
                await ViewUnloaded();

                if (cancellationTokenSource != null)
                {
                    await cancellationTokenSource.CancelAsync();
                    cancellationTokenSource.Dispose();
                }
            }
            catch (Exception e)
            {
                HandleLifetimeException(nameof(ViewUnloaded), e);
            }
        };
    }
}

[Flags]
public enum ExecutionFlags
{
    None = 0,
    EnqueueIfBusy = 1,
}

public abstract partial class BusyViewModelBase : ReactiveViewModelBase
{
    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    public bool IsNotBusy => !IsBusy;

    private Task? currentTask;
    private readonly SemaphoreSlim executionLock = new(1, 1);

    protected async Task ExecuteBusyTaskAsync(
        Func<CancellationToken, Task> task,
        IExceptionHandler? exceptionHandler = null,
        ExecutionFlags flags = ExecutionFlags.None,
        CancellationToken cancellationToken = default)
    {
        await executionLock.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!flags.HasFlag(ExecutionFlags.EnqueueIfBusy) && IsBusy) return;

            Task taskToWait;
            if (currentTask is { IsCompleted: false })
            {
                taskToWait = currentTask;
                currentTask = currentTask.ContinueWith(
                    async _ =>
                    {
                        try { await task(cancellationToken); }
                        catch when (cancellationToken.IsCancellationRequested) { }
                    },
                    TaskContinuationOptions.RunContinuationsAsynchronously);
            }
            else
            {
                taskToWait = currentTask = task(cancellationToken);
            }

            try
            {
                IsBusy = true;
                await taskToWait;
            }
            catch (OperationCanceledException) { }
            catch (Exception e) when (exceptionHandler != null)
            {
                exceptionHandler.HandleException(e);
            }
            finally
            {
                IsBusy = currentTask is
                {
                    Status: TaskStatus.WaitingToRun or TaskStatus.Running or TaskStatus.WaitingForChildrenToComplete
                };
            }
        }
        finally
        {
            executionLock.Release();
        }
    }

    protected Task ExecuteBusyTaskAsync(
        Action<CancellationToken> action,
        IExceptionHandler? exceptionHandler = null,
        ExecutionFlags flags = ExecutionFlags.None,
        CancellationToken cancellationToken = default) => ExecuteBusyTaskAsync(
        token =>
        {
            action(token);
            return Task.CompletedTask;
        },
        exceptionHandler,
        flags,
        cancellationToken);
}