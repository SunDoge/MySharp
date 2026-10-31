using System.Collections.Concurrent;
using System.Text;

namespace MySharp;

/// <summary>Marks an ordinary UI function for the optional compact formatter.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ComponentAttribute : Attribute;

/// <summary>Optional metadata identifying a UI node factory.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class WidgetAttribute : Attribute;

/// <summary>An optional explicit identity overriding the default caller location.</summary>
public readonly record struct CallSiteId(ulong Value)
{
    private static readonly ConcurrentDictionary<(string File, int Line), ulong> Locations = new();

    internal static ulong FromSource(string file, int line) => Locations.GetOrAdd((file, line), static location =>
    {
        var hash = 14695981039346656037UL;
        foreach (var value in Encoding.UTF8.GetBytes(location.File)) hash = unchecked((hash ^ value) * 1099511628211UL);
        hash = unchecked((hash ^ 0xff) * 1099511628211UL);
        for (var shift = 0; shift < 32; shift += 8)
            hash = unchecked((hash ^ (byte)(location.Line >> shift)) * 1099511628211UL);
        return hash;
    });

    public static implicit operator CallSiteId(ulong value) => new(value);
}
