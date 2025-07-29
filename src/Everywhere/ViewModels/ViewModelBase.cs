using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Utils;
using ShadUI;

namespace Everywhere.ViewModels;

public abstract class ReactiveViewModelBase : ObservableValidator
{
    protected static DialogManager DialogManager { get; } = ServiceLocator.Resolve<DialogManager>();
    protected static ToastManager ToastManager { get; } = ServiceLocator.Resolve<ToastManager>();

    protected static AnonymousExceptionHandler DialogExceptionHandler => new((exception, message, source) =>
        DialogManager.CreateDialog($"[{source}] {message ?? "Error"}", exception.GetFriendlyMessage().ToString() ?? "Unknown error"));

    protected static AnonymousExceptionHandler ToastExceptionHandler => new((exception, message, source) =>
        ToastManager.CreateToast($"[{source}] {message ?? "Error"}")
            .WithContent(exception.GetFriendlyMessage().ToTextBlock())
            .DismissOnClick()
            .ShowError());

    protected IClipboard Clipboard { get; private set; } = ServiceLocator.Resolve<IClipboard>();

    protected IStorageProvider StorageProvider { get; private set; } = ServiceLocator.Resolve<IStorageProvider>();

    protected internal virtual Task ViewLoaded(CancellationToken cancellationToken) => Task.CompletedTask;

    protected internal virtual Task ViewUnloaded() => Task.CompletedTask;

    protected virtual IExceptionHandler? LifetimeExceptionHandler => null;

    private void HandleLifetimeException(string stage, Exception e)
    {
        var handler = LifetimeExceptionHandler ?? DialogExceptionHandler;
        handler.HandleException(e, $"Lifetime Exception: [{stage}]");
    }

    public void Bind(Control target)
    {
        target.DataContext = this;

        var topLevel = TopLevel.GetTopLevel(target);
        if (topLevel is not null)
        {
            if (topLevel.Clipboard is { } clipboard) Clipboard = clipboard;
            StorageProvider = topLevel.StorageProvider;
        }

        var cancellationTokenSource = new ReusableCancellationTokenSource();
        target.Loaded += async (_, _) =>
        {
            try
            {
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
                cancellationTokenSource.Cancel();
                await ViewUnloaded();
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
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
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