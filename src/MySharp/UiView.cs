using System.Runtime.CompilerServices;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;

namespace MySharp;

/// <summary>Runs declarations on demand and retains Avalonia controls between builds.</summary>
public sealed class UiView : ContentControl, IDisposable
{
    private Action<UiContext>? build;
    private readonly UiContext context;
    private int queued;
    private volatile bool disposed;
    private readonly Action flush;

    public UiView(Action<UiContext> build)
    {
        this.build = build;
        flush = Flush;
        var root = new Grid();
        Content = root;
        context = new UiContext(root, Invalidate);
        try { Rebuild(); }
        catch (Exception buildError)
        {
            disposed = true;
            this.build = null;
            try { context.Clear(); }
            catch (Exception cleanupError) { throw new AggregateException("Initial build and cleanup failed.", buildError, cleanupError); }
            finally { Content = null; }
            throw;
        }
    }

    /// <summary>Safe to request from a background task; model mutation remains the caller's responsibility.</summary>
    public void Invalidate()
    {
        if (disposed || Interlocked.Exchange(ref queued, 1) != 0) return;
        Dispatcher.UIThread.Post(flush);
    }
    private void Flush()
    {
        Interlocked.Exchange(ref queued, 0);
        if (!disposed) Rebuild();
    }

    public bool ProfilingEnabled { get => context.ProfilingEnabled; set => context.ProfilingEnabled = value; }
    public UiFrameProfile LastProfile => context.LastProfile;
    public UiStorageStats StorageStats => context.StorageStats;
    public event Action<UiFrameProfile>? FrameMeasured;

    public void Rebuild()
    {
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(disposed, this);
        context.Build(build!);
        if (context.ProfilingEnabled) FrameMeasured?.Invoke(context.LastProfile);
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs args)
    {
        base.OnAttachedToVisualTree(args);
        context.Window.Attach(TopLevel.GetTopLevel(this) as Window);
    }
    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs args)
    {
        context.Window.Attach(null);
        base.OnDetachedFromVisualTree(args);
    }
    public void Dispose()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (disposed) return;
        disposed = true;
        build = null;
        try { context.Clear(); }
        finally { Content = null; }
    }
}

public sealed partial class UiContext
{
    private readonly Panel root;
    public UiWindow Window { get; } = new();
    private readonly Action invalidate;
    private readonly Dictionary<ulong, Node> nodes = [];
    private readonly HashSet<ulong> seen = [];
    private readonly Dictionary<Node, int> clicks = [];
    private readonly HashSet<Node> polled = [];
    private readonly HashSet<Node> expanded = [];
    private Frame? frame;
    private readonly Frame rootFrame;
    private readonly List<ulong> removedPaths = [];
    private readonly List<Node> removedClicks = [];
    private bool building;
    private bool consumed;
    private ulong scope;
    private readonly Dictionary<ulong, ScopeEntry> scopes = [];
    private readonly List<ulong> removedScopes = [];
    private sealed class ScopeEntry(NodeAddress address, int seen)
    {
        internal readonly NodeAddress Address = address;
        internal int Seen = seen;
    }
    internal int Generation { get; set; }
    private ElementOwner owner;
    private readonly List<Node> frameNodes = [];

    internal UiContext(Panel root, Action invalidate)
    {
        this.root = root;
        this.invalidate = invalidate;
        rootFrame = new Frame(0, root);
        owner = new ElementOwner(this);
    }

    internal sealed class Frame(ulong id, Panel panel)
    {
        public readonly ulong Id = id;
        public readonly Panel Panel = panel;
        public readonly List<Node> Next = [];
        public readonly HashSet<Control> Desired = [];
        public readonly Dictionary<(ulong Scope, ulong Site), int> Occurrences = [];
        public void Begin() { Next.Clear(); Desired.Clear(); Occurrences.Clear(); }
    }

    internal void Validate(int generation)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (!building || generation != Generation)
            throw new InvalidOperationException("An Element is only valid during the build that created it.");
    }

    internal void Build(Action<UiContext> build)
    {
        if (building) throw new InvalidOperationException("UI builds cannot be reentered.");
        var profileStart = ProfilingEnabled ? Stopwatch.GetTimestamp() : 0;
        var allocatedStart = ProfilingEnabled ? GC.GetAllocatedBytesForCurrentThread() : 0;
        reconciliationTicks = 0; createdNodes = 0;
        if (Generation == int.MaxValue)
        {
            owner.Context = null;
            owner = new ElementOwner(this);
            Generation = 0;
        }
        Generation++;
        frameNodes.Clear();
        seen.Clear();
        expanded.Clear();
        consumed = false;
        polled.Clear();
        scope = 0;
        building = true;
        rootFrame.Begin();
        frame = rootFrame;
        reads.Clear();
        var previousContext = Current;
        try
        {
            Current = this;
            try { build(this); }
            finally { Current = previousContext; }
            Reconcile(frame);
            removedPaths.Clear();
            foreach (var node in nodes.Values)
            {
                if (!seen.Contains(node.Id)) { removedPaths.Add(node.Id); continue; }
                if (node.Container is { } container && !expanded.Contains(node))
                {
                    node.Frame ??= new Frame(node.Id, container);
                    node.Frame.Begin(); Reconcile(node.Frame);
                }
                node.End(Generation);
            }
            var cleanupErrors = new CleanupErrors();
            foreach (var path in removedPaths)
            {
                var removed = nodes[path]; nodes.Remove(path);
                try { removed.Dispose(); } catch (Exception error) { cleanupErrors.Add(error); }
            }
            try { SweepResources(); } catch (Exception error) { cleanupErrors.Add(error); }
            removedScopes.Clear();
            foreach (var pair in scopes) if (pair.Value.Seen != Generation) removedScopes.Add(pair.Key);
            foreach (var key in removedScopes) scopes.Remove(key);
            TrimRetainedStorage();
            cleanupErrors.ThrowIfAny();
            if (ProfilingEnabled)
            {
                var writes = 0;
                foreach (var node in nodes.Values) writes += node.PropertyWrites;
                LastProfile = new UiFrameProfile(Stopwatch.GetElapsedTime(profileStart),
                    TimeSpan.FromSeconds((double)reconciliationTicks / Stopwatch.Frequency),
                    GC.GetAllocatedBytesForCurrentThread() - allocatedStart, seen.Count, createdNodes, writes);
            }
        }
        finally
        {
            removedClicks.Clear();
            foreach (var node in clicks.Keys)
                if (!polled.Contains(node) || nodes.GetValueOrDefault(node.Id) != node) removedClicks.Add(node);
            foreach (var node in removedClicks) clicks.Remove(node);
            removedClicks.Clear(); RetainedStorage.Trim(removedClicks);
            frame = null;
            building = false;
        }
        // Inline Clicked() can change data after an earlier Text declaration.
        if (consumed) invalidate();
    }

    internal bool IsElementValid(int slot, int generation) => Dispatcher.UIThread.CheckAccess()
        && building && generation == Generation && (uint)slot < (uint)frameNodes.Count;

    internal Node ResolveElement(int slot, int generation)
    {
        Validate(generation);
        if ((uint)slot >= (uint)frameNodes.Count) throw new InvalidOperationException("Invalid element slot.");
        return frameNodes[slot];
    }

    internal bool IsBuilding => building;
    internal bool IsActive(Node node) => nodes.GetValueOrDefault(node.Id) == node;

    internal Element Add(string kind, string? key, CallSiteId site, string file, int line, Func<Control>? create = null, bool wrap = true)
    {
        Validate(Generation);
        var parent = frame!;
        var identity = site.Value != 0 ? IdentityKey.Site(site.Value, key) :
            key is not null ? IdentityKey.Explicit(key) : CallerIdentity(file, line);
        var address = new NodeAddress(parent.Id, scope, identity);
        var id = address.Id();
        if (!seen.Add(id)) throw new InvalidOperationException($"Duplicate UI identity {id:x16}; use Scope(item.Id, ...) for repeated explicit keys.");
        var found = nodes.TryGetValue(id, out var node);
        if (found && node!.Address != address) throw new InvalidOperationException("UI identity hash collision; provide an explicit key or scope.");
        if (!found || node!.Kind != kind) node = CreateNode(id, address, kind, create, wrap);
        Reset(node);
        parent.Next.Add(node);
        parent.Desired.Add(node.Box);
        var slot = frameNodes.Count;
        frameNodes.Add(node);
        return new Element(owner, slot, Generation);
    }

    private Node CreateNode(ulong id, NodeAddress address, string kind, Func<Control>? create, bool wrap)
    {
        createdNodes++;
        if (nodes.TryGetValue(id, out var previous))
        {
            removedPaths.Clear();
            foreach (var candidate in nodes.Values)
            {
                var parent = candidate.Address.Parent;
                while (parent != 0)
                {
                    if (parent == id) { removedPaths.Add(candidate.Id); break; }
                    if (!nodes.TryGetValue(parent, out var ancestor)) break;
                    parent = ancestor.Address.Parent;
                }
            }
            var errors = new CleanupErrors();
            foreach (var descendant in removedPaths)
            {
                var removed = nodes[descendant]; nodes.Remove(descendant);
                try { removed.Dispose(); } catch (Exception error) { errors.Add(error); }
            }
            nodes.Remove(id);
            try { previous.Dispose(); } catch (Exception error) { errors.Add(error); }
            errors.ThrowIfAny();
        }
        Control inner = create?.Invoke() ?? (kind switch
        {
            "column" => new StackPanel(),
            "scroll" => new ScrollViewer { Content = new StackPanel(), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled },
            "row" => new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal },
            "grid" => new Grid(), "text" => new TextBlock(), "button" => new Button(),
            "input" => new TextBox(), "space" => new Border(),
            _ => throw new ArgumentException($"Unknown node kind: {kind}")
        });
        if (inner is TopLevel) throw new InvalidOperationException("TopLevel controls need MySharpApp.CreateWindow, not Ui.Control.");
        var node = new Node { Kind = kind, Id = id, Address = address, Inner = inner, Box = wrap ? new Border { Child = inner } : inner };
        if (inner is Button button)
        {
            void Click(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
            {
                if (nodes.GetValueOrDefault(id) != node || building) return;
                if (node.Click is { } action) action();
                else clicks[node] = clicks.GetValueOrDefault(node) + 1;
                invalidate();
            }
            button.Click += Click;
            node.Cleanup = () => button.Click -= Click;
        }
        if (inner is TextBox input)
        {
            void Changed(object? sender, TextChangedEventArgs args)
            {
                if (building || nodes.GetValueOrDefault(id) != node) return;
                var value = input.Text ?? "";
                if (value == node.LastText) return;
                node.LastText = value;
                node.TextChanged?.Invoke(value);
                invalidate();
            }
            input.TextChanged += Changed;
            node.Cleanup = () => input.TextChanged -= Changed;
        }
        nodes[id] = node;
        return node;
    }

    private IdentityKey CallerIdentity(string file, int line)
    {
        var site = CallSiteId.FromSource(file, line);
        var identity = (scope, site);
        var occurrence = frame!.Occurrences.GetValueOrDefault(identity);
        frame.Occurrences[identity] = occurrence + 1;
        return IdentityKey.Caller(file, line, occurrence, site);
    }

    /// <summary>Scopes an ordinary component call by its caller file, line and same-line occurrence.</summary>
    public void Scope(Action build, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        Validate(Generation);
        InScope(CallerIdentity(file, line), build);
    }

    /// <summary>Adds an instance namespace without adding a layout/control node.</summary>
    public void Scope(string key, Action build)
    {
        ArgumentNullException.ThrowIfNull(key);
        InScope(IdentityKey.StringScope(key), build);
    }

    public void Scope(long key, Action build) => InScope(IdentityKey.LongScope(key), build);
    public void Scope(Guid key, Action build) => InScope(IdentityKey.GuidScope(key), build);

    /// <summary>Disambiguates separate invocations of the same component.</summary>
    public void Scope(CallSiteId site, Action build)
    {
        if (site.Value == 0) throw new ArgumentException("A call-site scope needs a nonzero ID.", nameof(site));
        InScope(IdentityKey.Site(site.Value), build);
    }

    private ulong EnterScope(IdentityKey key)
    {
        Validate(Generation);
        var previous = scope;
        var address = new NodeAddress(frame!.Id, previous, key);
        var id = address.Id(scope: true);
        if (scopes.TryGetValue(id, out var entry))
        {
            if (entry.Address != address) throw new InvalidOperationException("Scope identity hash collision; provide another explicit key.");
            if (entry.Seen == Generation) throw new InvalidOperationException("Duplicate sibling scope. Use a unique business ID or a call-site scope.");
            entry.Seen = Generation;
        }
        else scopes.Add(id, new ScopeEntry(address, Generation));
        scope = id;
        return previous;
    }
    private void InScope(IdentityKey key, Action build)
    {
        ArgumentNullException.ThrowIfNull(build);
        var previous = EnterScope(key);
        try { build(); }
        finally { scope = previous; }
    }
    private void InScope<T>(IdentityKey key, T state, Action<UiContext, T> build)
    {
        ArgumentNullException.ThrowIfNull(build);
        var previous = EnterScope(key);
        try { build(this, state); }
        finally { scope = previous; }
    }
    public void Scope<T>(T state, Action<UiContext, T> build,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        Validate(Generation);
        InScope(CallerIdentity(file, line), state, build);
    }
    public void Scope<T>(string key, T state, Action<UiContext, T> build) => InScope(IdentityKey.StringScope(key), state, build);
    public void Scope<T>(long key, T state, Action<UiContext, T> build) => InScope(IdentityKey.LongScope(key), state, build);
    public void Scope<T>(Guid key, T state, Action<UiContext, T> build) => InScope(IdentityKey.GuidScope(key), state, build);

    private static void Reset(Node node)
    {
        node.PropertyWrites = 0;
        node.ExplicitCell = false;
        node.Click = null;
        node.TextChanged = null;
    }

    private (Frame Frame, ulong Scope) EnterChildren(Node node)
    {
        var panel = node.Container;
        if (panel is null)
        {
            panel = node.Body ??= new Grid();
            if (node.Inner is ContentControl content)
                node.Set<object?>(content, ContentControl.ContentProperty, panel, Generation);
            else if (node.Inner is Decorator decorator) node.Set<Control?>(decorator, Decorator.ChildProperty, panel, Generation);
            else throw new InvalidOperationException("Children requires a Panel, ContentControl or Decorator.");
        }
        else if (node.Inner is ContentControl content)
            node.Set<object?>(content, ContentControl.ContentProperty, panel, Generation);
        else if (node.Inner is Decorator decorator)
            node.Set<Control?>(decorator, Decorator.ChildProperty, panel, Generation);
        node.Body = panel;
        if (node.Frame is { } oldFrame && oldFrame.Panel != panel) node.Frame = null;
        if (!expanded.Add(node)) throw new InvalidOperationException("Children can only be declared once per node per build.");
        var previous = (frame!, scope);
        var nested = node.Frame ??= new Frame(node.Id, panel);
        nested.Begin();
        frame = nested;
        scope = 0;
        return previous;
    }
    internal void Children(Node node, Action build)
    {
        ArgumentNullException.ThrowIfNull(build);
        var previous = EnterChildren(node);
        try { build(); Reconcile(frame!); }
        finally { frame = previous.Frame; scope = previous.Scope; }
    }
    internal void Children<T>(Node node, T state, Action<UiContext, T> build)
    {
        ArgumentNullException.ThrowIfNull(build);
        var previous = EnterChildren(node);
        try { build(this, state); Reconcile(frame!); }
        finally { frame = previous.Frame; scope = previous.Scope; }
    }

    private static void Sync(Frame frame, int generation)
    {
        if (frame.Panel is Grid)
            for (var i = 0; i < frame.Next.Count; i++)
            {
                var node = frame.Next[i];
                if (node.ExplicitCell) continue;
                node.Set(node.Box, Grid.RowProperty, 0, generation);
                node.Set(node.Box, Grid.ColumnProperty, i, generation);
                node.Set(node.Box, Grid.RowSpanProperty, 1, generation);
                node.Set(node.Box, Grid.ColumnSpanProperty, 1, generation);
            }
        var unchanged = frame.Panel.Children.Count == frame.Next.Count;
        if (unchanged)
        {
            for (var i = 0; i < frame.Next.Count; i++)
                if (frame.Panel.Children[i] != frame.Next[i].Box) { unchanged = false; break; }
            if (unchanged) return;
        }
        var desired = frame.Desired;
        for (var i = frame.Panel.Children.Count - 1; i >= 0; i--)
            if (!desired.Contains(frame.Panel.Children[i])) frame.Panel.Children.RemoveAt(i);
        for (var i = 0; i < frame.Next.Count; i++)
        {
            var child = frame.Next[i].Box;
            if (i < frame.Panel.Children.Count && frame.Panel.Children[i] == child) continue;
            var oldIndex = frame.Panel.Children.IndexOf(child);
            if (oldIndex >= 0) frame.Panel.Children.Move(oldIndex, i);
            else
            {
                if (child.Parent is Panel oldParent) oldParent.Children.Remove(child);
                frame.Panel.Children.Insert(i, child);
            }
        }


    }

    internal bool ConsumeClick(Node node)
    {
        polled.Add(node);
        if (!clicks.TryGetValue(node, out var count)) return false;
        if (count == 1) clicks.Remove(node);
        else clicks[node] = count - 1;
        consumed = true;
        return true;
    }

    internal void Clear()
    {
        owner.Context = null;
        frameNodes.Clear();
        var errors = new CleanupErrors();
        try { ClearResources(); } catch (Exception error) { errors.Add(error); }
        try { Window.Dispose(); } catch (Exception error) { errors.Add(error); }
        foreach (var node in nodes.Values)
            try { node.Dispose(); } catch (Exception error) { errors.Add(error); }
        nodes.Clear();
        clicks.Clear();
        root.Children.Clear();
        rootFrame.Begin();
        seen.Clear(); polled.Clear(); expanded.Clear(); scopes.Clear();
        removedPaths.Clear(); removedClicks.Clear(); removedScopes.Clear();
        errors.ThrowIfAny();
    }
}
