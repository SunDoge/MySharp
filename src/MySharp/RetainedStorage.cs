namespace MySharp;

public readonly record struct UiStorageStats(int Nodes, int NodeCapacity, int HandleCapacity,
    int Scopes, int ScopeCapacity, int Resources, int ResourceCapacity);

internal static class RetainedStorage
{
    internal static void Trim<T>(List<T> list)
    {
        if (list.Capacity > 4096 && list.Count < list.Capacity / 4)
            list.Capacity = Math.Max(32, list.Count * 2);
    }
    internal static void Trim<T>(HashSet<T> set)
    {
        var capacity = set.EnsureCapacity(0);
        if (capacity > 4096 && set.Count < capacity / 4) set.TrimExcess();
    }
    internal static void Trim<TKey, TValue>(Dictionary<TKey, TValue> dictionary) where TKey : notnull
    {
        var capacity = dictionary.EnsureCapacity(0);
        if (capacity > 4096 && dictionary.Count < capacity / 4) dictionary.TrimExcess();
    }
}

public sealed partial class UiContext
{
    internal UiStorageStats StorageStats => new(nodes.Count, nodes.EnsureCapacity(0), frameNodes.Capacity,
        scopes.Count, scopes.EnsureCapacity(0), resources.Count, resources.EnsureCapacity(0));
    private void TrimRetainedStorage()
    {
        RetainedStorage.Trim(nodes); RetainedStorage.Trim(seen); RetainedStorage.Trim(scopes);
        RetainedStorage.Trim(frameNodes); RetainedStorage.Trim(resources);
        removedPaths.Clear(); removedScopes.Clear();
        RetainedStorage.Trim(removedPaths); RetainedStorage.Trim(removedScopes);
    }
}
