using Avalonia.Threading;

namespace MySharp;

public enum UiTaskStatus { Idle, Running, Succeeded, Failed, Canceled }

/// <summary>A UI-thread task handle. Work runs on the thread pool; results are published on the dispatcher.</summary>
public sealed class UiTask<T> : ObservableState
{
    private UiTaskStatus status;
    private T? result;
    private Exception? error;
    private long version;
    private Func<CancellationToken, Task<T>>? work;
    private Operation? active;
    private bool runOnUi;
    private Task completion = Task.CompletedTask;
    private sealed class Operation(long version)
    {
        internal readonly long Version = version;
        internal readonly CancellationTokenSource Source = new();
        internal Task Cancellation = Task.CompletedTask;
    }

    public UiTaskStatus Status { get { Observe(); return status; } }
    public bool IsRunning => Status == UiTaskStatus.Running;
    public bool HasValue => Status == UiTaskStatus.Succeeded;
    public T? Result { get { Observe(); return result; } }
    public Exception? Error { get { Observe(); return error; } }
    /// <summary>Finishes after the result has been dispatched. Pump/await the UI loop; never block it.</summary>
    public Task Completion { get { Verify(); return completion; } }

    public void Run(Func<CancellationToken, Task<T>> run) => RunCore(run, false);
    /// <summary>Starts on the UI thread for async clipboard/picker/dialog APIs. Do not perform blocking CPU work in its synchronous prefix.</summary>
    public void RunOnUi(Func<CancellationToken, Task<T>> run) => RunCore(run, true);
    private void RunCore(Func<CancellationToken, Task<T>> run, bool ui)
    {
        Verify(); ArgumentNullException.ThrowIfNull(run);
        CancelOperation();
        work = run; runOnUi = ui;
        var operation = new Operation(++version);
        active = operation;
        status = UiTaskStatus.Running; result = default; error = null;
        Notify();
        completion = ui ? Execute(operation, run) : Task.Run(() => Execute(operation, run));
    }
    public void Restart()
    {
        Verify();
        if (work is null) throw new InvalidOperationException("The task has not been started.");
        RunCore(work, runOnUi);
    }
    public void Cancel()
    {
        Verify();
        if (status != UiTaskStatus.Running) return;
        version++;
        CancelOperation();
        status = UiTaskStatus.Canceled; result = default; error = null;
        Notify();
    }
    private void CancelOperation()
    {
        var previous = active;
        active = null;
        if (previous is not null) previous.Cancellation = previous.Source.CancelAsync();
    }
    private async Task Execute(Operation operation, Func<CancellationToken, Task<T>> run)
    {
        T? value = default;
        Exception? failure = null;
        var outcome = UiTaskStatus.Succeeded;
        try
        {
            operation.Source.Token.ThrowIfCancellationRequested();
            value = await run(operation.Source.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (operation.Source.IsCancellationRequested) { outcome = UiTaskStatus.Canceled; }
        catch (Exception exception) { outcome = UiTaskStatus.Failed; failure = exception; }
        // A result may race with cancel, restart, dependency changes or view disposal.
        try
        {
            if (!Disposed)
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (Disposed || operation.Version != version) return;
                    active = null;
                    status = outcome; result = value; error = failure;
                    Notify();
                });
        }
        finally
        {
            try { await operation.Cancellation.ConfigureAwait(false); }
            catch (Exception exception)
            {
                if (!Disposed)
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (Disposed || operation.Version != version) return;
                        status = UiTaskStatus.Failed; error = exception; Notify();
                    });
            }
            operation.Source.Dispose();
        }
    }
    protected override void DisposeCore()
    {
        version++; CancelOperation();
        work = null; result = default; error = null; completion = Task.CompletedTask;
    }
}
