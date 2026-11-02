using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Media;

namespace MySharp;

/// <summary>Marks a UI method for the optional compact-chain formatter.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class UiComponentAttribute : Attribute;

/// <summary>Factories attach nodes to the context's current parent immediately.</summary>
public static partial class Ui
{
    [Widget]
    public static Element Column(UiContext cx, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) => cx.Add("column", key, site, file, line);
    [Widget]
    public static Element Scroll(UiContext cx, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) => cx.Add("scroll", key, site, file, line);
    [Widget]
    public static Element Row(UiContext cx, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) => cx.Add("row", key, site, file, line);
    [Widget]
    public static Element Grid(UiContext cx, string columns, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        var element = cx.Add("grid", key, site, file, line);
        var node = element.Node;
        var grid = (Grid)node.Inner;
        if (node.Columns != columns)
        {
            grid.ColumnDefinitions = new ColumnDefinitions(columns);
            node.Columns = columns;
        }
        return element;
    }
    [Widget]
    public static Element Text(UiContext cx, string text, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        var element = cx.Add("text", key, site, file, line);
        var node = element.Node;
        node.Set(node.Inner, TextBlock.TextProperty, text, cx.Generation);
        return element;
    }
    [Widget]
    public static Element Button(UiContext cx, string text, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        var element = cx.Add("button", key, site, file, line);
        var node = element.Node;
        node.Set<object?>(node.Inner, ContentControl.ContentProperty, text, cx.Generation);
        return element;
    }
    [Widget]
    public static Element TextInput(UiContext cx, string value, Action<string> changed, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        var element = cx.Add("input", key, site, file, line);
        var node = element.Node;
        var box = (TextBox)node.Inner;
        node.TextChanged = changed;
        node.LastText = value;
        // Do not assign equal text: preserve the selection, caret and IME state.
        node.Set<string?>(box, TextBox.TextProperty, value, cx.Generation);
        return element;
    }
    [Widget]
    public static Element Space(UiContext cx, double height, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) => cx.Add("space", null, site, file, line).Height(height);
}

/// <summary>A build-local handle. Keep model data, not Elements, between builds.</summary>
public readonly partial struct Element
{
    private readonly ElementOwner? owner;
    private readonly int slot;
    private readonly int generation;
    internal Node Node => owner?.Resolve(slot, generation) ?? throw new InvalidOperationException("The default Element is absent.");
    private UiContext context => owner?.Context ?? throw new InvalidOperationException("The element's view is no longer available.");
    internal Element(ElementOwner owner, int slot, int generation)
    {
        this.owner = owner; this.slot = slot; this.generation = generation;
    }
    public bool IsValid => owner?.Context is { } current && current.IsElementValid(slot, generation);
    public Element Set<T>(StyledProperty<T> property, T value)
    {
        var node = Node; node.Set(node.Inner, property, value, generation); return this;
    }
    public Element Set<T>(DirectPropertyBase<T> property, T value)
    {
        var node = Node; node.Set(node.Inner, property, value, generation); return this;
    }
    private Element Box<T>(StyledProperty<T> property, T value)
    {
        var node = Node; node.Set(node.Box, property, value, generation); return this;
    }
    public Element Gap(double value)
    {
        var node = Node;
        if (node.Inner is not StackPanel) throw new InvalidOperationException("Gap requires a Row or Column.");
        node.Set(node.Inner, StackPanel.SpacingProperty, value, generation); return this;
    }
    public Element Padding(double all) => Box(Avalonia.Controls.Border.PaddingProperty, new Thickness(all));
    public Element Padding(double horizontal, double vertical) => Box(Avalonia.Controls.Border.PaddingProperty, new Thickness(horizontal, vertical));
    public Element Margin(double all) => Box(Layoutable.MarginProperty, new Thickness(all));
    public Element Width(double value) => Box(Layoutable.WidthProperty, value);
    public Element Height(double value) => Box(Layoutable.HeightProperty, value);
    public Element Fill() => Box(Layoutable.HorizontalAlignmentProperty, HorizontalAlignment.Stretch)
        .Box(Layoutable.VerticalAlignmentProperty, VerticalAlignment.Stretch);
    public Element Center() => Set(Layoutable.HorizontalAlignmentProperty, HorizontalAlignment.Center)
        .Set(Layoutable.VerticalAlignmentProperty, VerticalAlignment.Center);
    public Element Background(string color) => Box<IBrush?>(Avalonia.Controls.Border.BackgroundProperty, UiResources.Brush(color));
    public Element Border(double width, string color) => Box(Avalonia.Controls.Border.BorderThicknessProperty, new Thickness(width))
        .Box<IBrush?>(Avalonia.Controls.Border.BorderBrushProperty, UiResources.Brush(color));
    public Element Radius(double value) => Box(Avalonia.Controls.Border.CornerRadiusProperty, new CornerRadius(value));
    public Element FontSize(double value)
    {
        var node = Node;
        if (node.Inner is TextBlock) node.Set(node.Inner, TextBlock.FontSizeProperty, value, generation);
        else if (node.Inner is Avalonia.Controls.Primitives.TemplatedControl) node.Set(node.Inner, Avalonia.Controls.Primitives.TemplatedControl.FontSizeProperty, value, generation);
        else throw new InvalidOperationException("FontSize requires text or an input control.");
        return this;
    }
    public Element Wrap()
    {
        var node = Node;
        if (node.Inner is not TextBlock) throw new InvalidOperationException("Wrap requires Text.");
        return Set(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
    }
    public Element Bold() => Weight(FontWeight.SemiBold);
    public Element Weight(FontWeight value)
    {
        var node = Node;
        if (node.Inner is TextBlock) return Set(TextBlock.FontWeightProperty, value);
        if (node.Inner is Avalonia.Controls.Primitives.TemplatedControl) return Set(Avalonia.Controls.Primitives.TemplatedControl.FontWeightProperty, value);
        throw new InvalidOperationException("Weight requires text or an input control.");
    }
    public Element TextColor(string color)
    {
        var node = Node;
        if (node.Inner is TextBlock) return Set<IBrush?>(TextBlock.ForegroundProperty, UiResources.Brush(color));
        if (node.Inner is Lucide.Avalonia.LucideIcon) return Set<IBrush?>(Lucide.Avalonia.LucideIcon.ForegroundProperty, UiResources.Brush(color));
        if (node.Inner is Avalonia.Controls.Primitives.TemplatedControl) return Set<IBrush?>(Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty, UiResources.Brush(color));
        throw new InvalidOperationException("TextColor requires text or an input control.");
    }
    public Element Disabled(bool value = true) => Box(InputElement.IsEnabledProperty, !value);
    public Element Label(string name) => Set(Avalonia.Automation.AutomationProperties.NameProperty, name);
    public Element Placeholder(string value)
    {
        var node = Node;
        if (node.Inner is not TextBox) throw new InvalidOperationException("Placeholder requires TextInput.");
        return Set<string?>(TextBox.PlaceholderTextProperty, value);
    }
    public Element Primary()
    {
        var node = Node;
        if (node.Inner is not Button button) throw new InvalidOperationException("Primary requires Button.");
        return Class("accent");
    }
    public Element Flat()
    {
        var node = Node;
        if (node.Inner is not Button) throw new InvalidOperationException("Flat requires Button.");
        return Class("quiet").Set<IBrush?>(Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty, Brushes.Transparent)
            .Set<IBrush?>(Avalonia.Controls.Primitives.TemplatedControl.BorderBrushProperty, Brushes.Transparent)
            .Set(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Left);
    }
    public Element OnClick(Action action)
    {
        var node = Node;
        if (node.Inner is not Button) throw new InvalidOperationException("OnClick requires Button.");
        node.Click = action; return this;
    }
    public bool Clicked()
    {
        var node = Node;
        if (node.Inner is not Button) throw new InvalidOperationException("Clicked requires Button.");
        return context.ConsumeClick(Node);
    }
    public Element Children(Action build)
    {
        var node = Node; context.Children(node, build); return this;
    }
    public Element Children<T>(T state, Action<UiContext, T> build)
    {
        var node = Node; context.Children(node, state, build); return this;
    }
}

internal sealed class Node
{
    public required string Kind;
    public required ulong Id;
    public required NodeAddress Address;
    public required Control Inner;
    public required Control Box;
    public Panel? Body;
    public Panel? Container => Inner as Panel ?? (Inner as ContentControl)?.Content as Panel ?? (Inner as Decorator)?.Child as Panel;
    public readonly List<NativeSubscription> Subscriptions = [];
    public int RowsGeneration;
    public string? Rows;
    public bool ExplicitCell;
    public readonly Dictionary<string, int> Classes = [];
    private readonly List<string> removedClasses = [];
    public int PropertyWrites;
    public readonly List<PropertySlot> Properties = [];
    public void Set<T>(AvaloniaObject target, StyledProperty<T> property, T value, int generation)
    {
        foreach (var slot in Properties)
            if (slot.Target == target && slot.Property == property) { if (((StyledSlot<T>)slot).Set(value, generation)) PropertyWrites++; return; }
        var added = new StyledSlot<T>(target, property);
        Properties.Add(added); if (added.Set(value, generation)) PropertyWrites++;
    }
    public void Set<T>(AvaloniaObject target, DirectPropertyBase<T> property, T value, int generation)
    {
        foreach (var slot in Properties)
            if (slot.Target == target && slot.Property == property) { if (((DirectSlot<T>)slot).Set(value, generation)) PropertyWrites++; return; }
        var added = new DirectSlot<T>(target, property);
        Properties.Add(added); if (added.Set(value, generation)) PropertyWrites++;
    }
    public void End(int generation)
    {
        foreach (var slot in Properties) if (slot.Seen != generation && slot.Clear()) PropertyWrites++;
        foreach (var slot in Subscriptions) if (slot.Seen != generation) slot.Dispose();
        removedClasses.Clear();
        foreach (var pair in Classes) if (pair.Value != generation) removedClasses.Add(pair.Key);
        foreach (var name in removedClasses) { Inner.Classes.Remove(name); Classes.Remove(name); }
        if (Inner is Grid grid && Rows is not null && RowsGeneration != generation) { grid.RowDefinitions = new RowDefinitions(); Rows = null; }
    }
    public string? Columns;
    public string? LastText;
    public Action? Cleanup;
    public Action? Click;
    public Action<string>? TextChanged;
    public UiContext.Frame? Frame;
    private bool disposed;
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        var errors = new CleanupErrors();
        foreach (var subscription in Subscriptions)
            try { subscription.Dispose(); } catch (Exception error) { errors.Add(error); }
        var cleanup = Cleanup; Cleanup = null;
        try { cleanup?.Invoke(); } catch (Exception error) { errors.Add(error); }
        try { if (Box.Parent is Panel parent) parent.Children.Remove(Box); }
        catch (Exception error) { errors.Add(error); }
        try { if (Box is Border wrapper && !ReferenceEquals(Box, Inner)) wrapper.Child = null; }
        catch (Exception error) { errors.Add(error); }
        try { if (Inner is IDisposable disposable) disposable.Dispose(); }
        catch (Exception error) { errors.Add(error); }
        Click = null; TextChanged = null;
        errors.ThrowIfAny();
    }
}
