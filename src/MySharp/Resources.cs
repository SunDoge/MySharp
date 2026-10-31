using System.Runtime.CompilerServices;
using Avalonia.Threading;

namespace MySharp;

public sealed partial class UiContext
{
    [ThreadStatic] internal static UiContext? Current;
    private readonly HashSet<ObservableState> subscriptions = [];
    private readonly HashSet<ObservableState> reads = [];
    private readonly List<ObservableState> unusedSubscriptions = [];
    private readonly Dictionary<ResourceAddress, ResourceEntry> resources = [];
    private readonly List<ResourceAddress> unusedResources = [];
    private readonly record struct ResourceAddress(ulong Parent, ulong Scope, string Kind, IdentityKey Key);
    private sealed class ResourceEntry(IDisposable value, int generation)
    {
        internal readonly IDisposable Value = value;
        internal int Seen = generation;
    }

    internal void Track(ObservableState source)
    {
        reads.Add(source);
        if (subscriptions.Add(source)) source.Subscribe(this);
    }
    internal void StateChanged() => invalidate();

    private T Resource<T>(string kind, string? key, string file, int line, Func<T> create) where T : class, IDisposable =>
        Resource(kind, key, file, line, create, static factory => factory());

    private T Resource<T, TArg>(string kind, string? key, string file, int line,
        TArg argument, Func<TArg, T> create) where T : class, IDisposable
    {
        Validate(Generation);
        var identity = key is null ? CallerIdentity(file, line) : IdentityKey.Explicit(key);
        var path = new ResourceAddress(frame!.Id, scope, kind, identity);
        if (resources.TryGetValue(path, out var entry))
        {
            if (entry.Seen == Generation) throw new InvalidOperationException($"Duplicate resource identity: {path}");
            if (entry.Value is not T) throw new InvalidOperationException($"Resource type changed at {path}; use separate scopes or keys.");
            entry.Seen = Generation;
            return (T)entry.Value;
        }
        var value = create(argument);
        resources.Add(path, new ResourceEntry(value, Generation));
        return value;
    }

    /// <summary>Owns an IDisposable created once for the current scope; disposes it on unmount.</summary>
    public T Resource<T>(Func<T> initialize, string? key = null,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) where T : class, IDisposable =>
        Resource("owned", key, file, line, initialize);

    public State<T> State<T>(T initial, string? key = null,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Resource("state", key, file, line, initial, static value => new State<T>(value));

    public State<T> State<T>(Func<T> initialize, string? key = null,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Resource("state", key, file, line, initialize, static factory => new State<T>(factory()));

    /// <summary>Manual task; start from an event with Run(), retry with Restart(), or Cancel().</summary>
    public UiTask<T> Task<T>(string? key = null,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Resource("task", key, file, line, 0, static _ => new UiTask<T>());

    /// <summary>Starts once per dependency key, cancels the previous run, and ignores stale results.</summary>
    public UiTask<T> Task<TKey, T>(TKey dependency, Func<TKey, CancellationToken, System.Threading.Tasks.Task<T>> load,
        string? key = null, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        var resource = Resource("keyed-task", key, file, line, 0, static _ => new KeyedTask<TKey, T>());
        resource.Use(dependency, load);
        return resource.Handle;
    }

    private sealed class KeyedTask<TKey, T> : IDisposable
    {
        internal readonly UiTask<T> Handle = new();
        private bool initialized;
        private TKey? previous;
        internal void Use(TKey key, Func<TKey, CancellationToken, System.Threading.Tasks.Task<T>> load)
        {
            if (initialized && EqualityComparer<TKey>.Default.Equals(previous!, key)) return;
            initialized = true;
            previous = key;
            Handle.Run(token => load(key, token));
        }
        public void Dispose() => Handle.Dispose();
    }

    private void SweepResources()
    {
        unusedSubscriptions.Clear();
        foreach (var source in subscriptions) if (!reads.Contains(source)) unusedSubscriptions.Add(source);
        foreach (var source in unusedSubscriptions) { source.Unsubscribe(this); subscriptions.Remove(source); }
        unusedResources.Clear();
        foreach (var pair in resources) if (pair.Value.Seen != Generation) unusedResources.Add(pair.Key);
        var errors = new CleanupErrors();
        foreach (var key in unusedResources)
        {
            var value = resources[key].Value;
            resources.Remove(key);
            try { value.Dispose(); } catch (Exception error) { errors.Add(error); }
        }
        unusedSubscriptions.Clear(); unusedResources.Clear();
        RetainedStorage.Trim(unusedSubscriptions); RetainedStorage.Trim(unusedResources);
        errors.ThrowIfAny();
    }
    private void ClearResources()
    {
        foreach (var source in subscriptions) source.Unsubscribe(this);
        subscriptions.Clear(); reads.Clear();
        var errors = new CleanupErrors();
        foreach (var entry in resources.Values)
            try { entry.Value.Dispose(); } catch (Exception error) { errors.Add(error); }
        resources.Clear();
        unusedSubscriptions.Clear(); unusedResources.Clear();
        errors.ThrowIfAny();
    }
}
