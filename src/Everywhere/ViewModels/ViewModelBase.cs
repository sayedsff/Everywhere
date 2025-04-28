using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using SukiUI.Controls;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace Everywhere.ViewModels;

public abstract class ReactiveViewModelBase : ObservableValidator
{
    protected internal ISukiDialogManager DialogManager { get; } = ServiceLocator.Resolve<ISukiDialogManager>();
    protected internal ISukiToastManager ToastManager { get; } = ServiceLocator.Resolve<ISukiToastManager>();

    protected void DialogExceptionHandler(Exception exception, string? message = null, [CallerMemberName] object? source = null) =>
        DialogManager.TryShowDialog(
            new SukiDialog
            {
                Title = message ?? "Error",
                Content = exception.GetFriendlyMessage(),
            });

    protected void ToastExceptionHandler(Exception exception, string? message = null, [CallerMemberName] object? source = null) =>
        ToastManager.CreateToast()
            .SetType(NotificationType.Error)
            .SetTitle(message ?? "Error")
            .SetContent(exception.GetFriendlyMessage())
            .SetCanDismissByClicking()
            .Queue();

    protected internal virtual Task ViewLoaded(CancellationToken cancellationToken) => Task.CompletedTask;

    protected internal virtual Task ViewUnloaded() => Task.CompletedTask;

    protected virtual IExceptionHandler? LifetimeExceptionHandler => null;

    private void HandleLifetimeException(string stage, Exception e)
    {
        if (LifetimeExceptionHandler is not { } lifetimeExceptionHandler)
        {
            ToastManager.CreateToast()
                .SetType(NotificationType.Error)
                .SetTitle($"Lifetime Exception: [{stage}]")
                .SetContent(e.GetFriendlyMessage())
                .SetCanDismissByClicking()
                .Queue();
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

public abstract partial class BusyViewModelBase : ReactiveViewModelBase
{
    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    public bool IsNotBusy => !IsBusy;

    private Task? currentTask;
    private readonly SemaphoreSlim executionLock = new(1, 1);

    protected async Task ExecuteBusyTaskAsync(
        Func<Task> task,
        IExceptionHandler? exceptionHandler = null,
        bool enqueueIfBusy = false,
        CancellationToken cancellationToken = default)
    {
        await executionLock.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!enqueueIfBusy && IsBusy) return;

            Task taskToWait;
            if (currentTask is
                {
                    Status: TaskStatus.WaitingToRun or TaskStatus.Running or TaskStatus.WaitingForChildrenToComplete
                })
            {
                taskToWait = currentTask;
                currentTask = currentTask.ContinueWith(
                    async _ =>
                    {
                        try { await task(); }
                        catch when (cancellationToken.IsCancellationRequested) { }
                    },
                    TaskContinuationOptions.RunContinuationsAsynchronously);
            }
            else
            {
                taskToWait = currentTask = task();
            }

            try
            {
                IsBusy = true;
                await taskToWait;
            }
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
}