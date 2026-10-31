using System.Diagnostics;

namespace MySharp;

/// <summary>Opt-in UI-thread declaration/reconciliation measurements, excluding compositor/GPU work.</summary>
public readonly record struct UiFrameProfile(TimeSpan RebuildTime, TimeSpan ReconciliationTime,
    long AllocatedBytes, int Nodes, int CreatedNodes, int PropertyWrites);

public sealed partial class UiContext
{
    internal bool ProfilingEnabled;
    internal UiFrameProfile LastProfile;
    private long reconciliationTicks;
    private int createdNodes;
    private void Reconcile(Frame value)
    {
        var started = ProfilingEnabled ? Stopwatch.GetTimestamp() : 0;
        Sync(value, Generation);
        RetainedStorage.Trim(value.Next); RetainedStorage.Trim(value.Desired); RetainedStorage.Trim(value.Occurrences);
        if (ProfilingEnabled) reconciliationTicks += Stopwatch.GetTimestamp() - started;
    }
}
