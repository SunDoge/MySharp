using Avalonia.Threading;

namespace MySharp;

/// <summary>UI-thread observable with view-owned subscriptions. Reads during a build subscribe that view.</summary>
public abstract class ObservableState : IDisposable
{
    private readonly HashSet<UiContext> observers = [];
    protected bool Disposed { get; private set; }
    protected void Verify()
    {
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(Disposed, this);
    }
    protected void Observe() { Verify(); UiContext.Current?.Track(this); }
    protected void Notify()
    {
        foreach (var observer in observers) observer.StateChanged();
    }
    internal void Subscribe(UiContext context) => observers.Add(context);
    internal void Unsubscribe(UiContext context) => observers.Remove(context);
    public void Dispose()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (Disposed) return;
        Disposed = true;
        DisposeCore();
        observers.Clear();
    }
    protected virtual void DisposeCore() { }
}

public static class State
{
    /// <summary>Creates externally owned shared state. Dispose after its consumers have unmounted.</summary>
    public static State<T> Create<T>(T initial, IEqualityComparer<T>? comparer = null) => new(initial, comparer);
}

public sealed class State<T>(T initial, IEqualityComparer<T>? comparer = null) : ObservableState
{
    private T value = initial;
    private Action<T>? setter;
    internal Action<T> Setter => setter ??= next => Value = next;
    private readonly IEqualityComparer<T> equality = comparer ?? EqualityComparer<T>.Default;
    public T Value
    {
        get { Observe(); return value; }
        set
        {
            Verify();
            if (equality.Equals(this.value, value)) return;
            this.value = value;
            Notify();
        }
    }
    public void Mutate(Action<T> mutation) { Verify(); mutation(value); Notify(); }
    public void Update(Func<T, T> update) { Verify(); Value = update(value); }
    protected override void DisposeCore() => value = default!;
    public async Task SetAsync(T next) => await Dispatcher.UIThread.InvokeAsync(() => { Value = next; });
    public async Task UpdateAsync(Func<T, T> update) => await Dispatcher.UIThread.InvokeAsync(() => Update(update));
}
