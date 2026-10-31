using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MySharp;

public static partial class Ui
{
    /// <summary>Hosts an existing Avalonia control. The factory runs once per retained identity.</summary>
    [Widget]
    public static Element Control<T>(UiContext cx, Func<T> create, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) where T : Control =>
        cx.Add(NativeType<T>.Kind, key, site, file, line, create);

    /// <summary>Hosts a layout-sensitive native control without a decorating Border (e.g. GridSplitter).</summary>
    [Widget]
    public static Element Direct<T>(UiContext cx, Func<T> create, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) where T : Control =>
        cx.Add(NativeType<T>.DirectKind, key, site, file, line, create, wrap: false);

    [Widget]
    public static Element Direct<T>(UiContext cx, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) where T : Control, new() =>
        Direct(cx, static () => new T(), key, site, file, line);

    private static class NativeType<T> { internal static readonly string Kind = "native:" + typeof(T).AssemblyQualifiedName; internal static readonly string DirectKind = "direct:" + typeof(T).AssemblyQualifiedName; }

    [Widget]
    public static Element Control<T>(UiContext cx, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) where T : Control, new() =>
        Control(cx, static () => new T(), key, site, file, line);
}

public readonly partial struct Element
{
    /// <summary>Build-local access to native APIs. Native controls themselves must follow Avalonia thread/lifetime rules.</summary>
    public T Native<T>() where T : Control
    {
        var node = Node;
        return node.Inner as T ?? throw new InvalidOperationException($"Expected {typeof(T).Name}, found {node.Inner.GetType().Name}.");
    }
    /// <summary>Sets a property on the decorating outer control (or the direct control itself).</summary>
    public Element SetOuter<T>(StyledProperty<T> property, T value) => Box(property, value);

    /// <summary>Imperative escape hatch. Prefer Set/OnChanged for properties/events that participate in declaration lifetime.</summary>
    public Element Configure<T>(Action<T> configure) where T : Control
    {
        configure(Native<T>()); return this;
    }
    public Element On<T>(RoutedEvent<T> routedEvent, EventHandler<T> handler,
        RoutingStrategies routes = RoutingStrategies.Direct | RoutingStrategies.Bubble,
        bool handledEventsToo = false) where T : RoutedEventArgs
    {
        var node = Node;
        RoutedSubscription<T>? slot = null;
        foreach (var candidate in node.Subscriptions)
            if (candidate is RoutedSubscription<T> typed && ReferenceEquals(candidate.Key, routedEvent)) { slot = typed; break; }
        if (slot is null)
        {
            slot = new RoutedSubscription<T>(context, node, routedEvent);
            node.Subscriptions.Add(slot);
        }
        slot.Routing(routes, handledEventsToo);
        slot.Handler = handler; slot.Seen = generation; slot.Attach();
        return this;
    }
    public Element OnChanged<T>(StyledProperty<T> property, Action<T> handler) => Changed(property, handler);
    public Element OnChanged<T>(DirectPropertyBase<T> property, Action<T> handler) => Changed(property, handler);
    private Element Changed<T>(AvaloniaProperty<T> property, Action<T> handler)
    {
        var node = Node;
        ChangeSubscription<T>? slot = null;
        foreach (var candidate in node.Subscriptions)
            if (candidate is ChangeSubscription<T> typed && ReferenceEquals(candidate.Key, property)) { slot = typed; break; }
        if (slot is null)
        {
            slot = new ChangeSubscription<T>(context, node, property);
            node.Subscriptions.Add(slot);
        }
        slot.Handler = handler; slot.Seen = generation; slot.Attach();
        return this;
    }
}

internal abstract class NativeSubscription(object key) : IDisposable
{
    internal readonly object Key = key;
    internal int Seen;
    protected bool Attached;
    internal abstract void Attach();
    public abstract void Dispose();
}

internal sealed class RoutedSubscription<T>(UiContext context, Node node, RoutedEvent<T> routedEvent)
    : NativeSubscription(routedEvent) where T : RoutedEventArgs
{
    internal EventHandler<T>? Handler;
    private RoutingStrategies routes;
    private bool handledEventsToo;
    internal void Routing(RoutingStrategies value, bool handled)
    {
        if (routes == value && handledEventsToo == handled) return;
        if (Attached) { node.Inner.RemoveHandler(routedEvent, Invoke); Attached = false; }
        routes = value; handledEventsToo = handled;
    }
    internal override void Attach()
    {
        if (Attached) return;
        node.Inner.AddHandler(routedEvent, Invoke, routes, handledEventsToo);
        Attached = true;
    }
    private void Invoke(object? sender, T args)
    {
        if (!context.IsActive(node) || context.IsBuilding) return;
        Handler?.Invoke(sender, args);
        context.StateChanged();
    }
    public override void Dispose()
    {
        if (Attached) node.Inner.RemoveHandler(routedEvent, Invoke);
        Attached = false; Handler = null;
    }
}

internal sealed class ChangeSubscription<T>(UiContext context, Node node, AvaloniaProperty<T> property)
    : NativeSubscription(property)
{
    internal Action<T>? Handler;
    internal override void Attach()
    {
        if (Attached) return;
        node.Inner.PropertyChanged += Invoke;
        Attached = true;
    }
    private void Invoke(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property != property || !context.IsActive(node) || context.IsBuilding) return;
        var value = property is StyledProperty<T> styled ? node.Inner.GetValue(styled) : node.Inner.GetValue((DirectPropertyBase<T>)property);
        Handler?.Invoke(value);
        context.StateChanged();
    }
    public override void Dispose()
    {
        if (Attached) node.Inner.PropertyChanged -= Invoke;
        Attached = false; Handler = null;
    }
}
