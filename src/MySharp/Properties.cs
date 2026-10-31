using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using System.Collections.Concurrent;

namespace MySharp;

internal static class UiResources
{
    private static readonly ConcurrentDictionary<string, IBrush> Brushes = new(StringComparer.Ordinal);
    internal static IBrush Brush(string value)
    {
        if (Brushes.TryGetValue(value, out var cached)) return cached;
        var brush = new ImmutableSolidColorBrush(Color.Parse(value));
        // Dynamic color input must not grow a process-wide cache without a bound.
        if (Brushes.Count < 512) return Brushes.GetOrAdd(value, brush);
        return brush;
    }
}

internal abstract class PropertySlot(AvaloniaObject target, AvaloniaProperty property)
{
    internal readonly AvaloniaObject Target = target;
    internal readonly AvaloniaProperty Property = property;
    internal int Seen;
    internal bool Active;
    internal bool Clear()
    {
        if (!Active) return false;
        Target.ClearValue(Property);
        Active = false;
        return true;
    }
}

internal sealed class StyledSlot<T>(AvaloniaObject target, StyledProperty<T> property) : PropertySlot(target, property)
{
    internal bool Set(T value, int generation)
    {
        Seen = generation;
        // Read the actual property, too: native input/bindings may have changed it.
        var baseValue = Target.GetBaseValue(property);
        var actual = baseValue.HasValue ? baseValue.Value : Target.GetValue(property);
        var changed = !Active || !EqualityComparer<T>.Default.Equals(actual, value);
        if (changed) Target.SetValue(property, value);
        Active = true;
        return changed;
    }
}

internal sealed class DirectSlot<T>(AvaloniaObject target, DirectPropertyBase<T> property) : PropertySlot(target, property)
{
    internal bool Set(T value, int generation)
    {
        Seen = generation;
        var changed = !Active || !EqualityComparer<T>.Default.Equals(Target.GetValue(property), value);
        if (changed) Target.SetValue(property, value);
        Active = true;
        return changed;
    }
}
