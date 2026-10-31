using System.Diagnostics;
using System.Reflection.Metadata;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.VisualTree;

[assembly: MetadataUpdateHandler(typeof(MySharp.HotReloadUpdateHandler))]

namespace MySharp;

/// <summary>Opt-in development bridge. Does no work on normal frames and never registers or retains views.</summary>
public static class MySharpHotReload
{
    private static int enabled, queued;
    /// <summary>True after Enable succeeds in a runtime supporting metadata updates.</summary>
    public static bool IsEnabled => Volatile.Read(ref enabled) != 0;
    /// <summary>The last refresh error, cleared at the start of the next refresh. Inspect on the UI thread.</summary>
    public static Exception? LastError { get; private set; }

    /// <summary>Call before starting the app, normally inside #if DEBUG. Returns false outside Hot Reload-capable development sessions.</summary>
    public static bool Enable()
    {
        if (!MetadataUpdater.IsSupported) return false;
        Volatile.Write(ref enabled, 1);
        return true;
    }

    /// <summary>Refreshes open desktop windows on the dispatcher. Calls coalesce; no timer or background polling is installed.</summary>
    public static void Refresh()
    {
        if (Interlocked.Exchange(ref queued, 1) != 0) return;
        Dispatcher.UIThread.Post(static () =>
        {
            Interlocked.Exchange(ref queued, 0);
            LastError = null;
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                foreach (var window in desktop.Windows.ToArray()) RefreshWindow(window);
        });
    }

    /// <summary>Explicit refresh for custom hosts that do not use the classic desktop lifetime.</summary>
    public static void Refresh(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        Dispatcher.UIThread.Post(() => { LastError = null; RefreshWindow(window); });
    }

    private static void RefreshWindow(Window window)
    {
        if (!window.IsVisible) return;
        // Snapshot before rebuilding: parents may remove/dispose child template views.
        var views = window.GetVisualDescendants().OfType<UiView>().ToArray();
        foreach (var view in views)
        {
            if (view.Content is null || !ReferenceEquals(TopLevel.GetTopLevel(view), window)) continue;
            try { view.Rebuild(); }
            catch (Exception error)
            {
                LastError = error;
                Trace.TraceError("MySharp Hot Reload refresh failed: {0}", error);
            }
        }
    }
}

internal static class HotReloadUpdateHandler
{
    // This callback is discovered by the runtime only when applying a metadata update.
    public static void UpdateApplication(Type[]? updatedTypes)
    {
        if (MySharpHotReload.IsEnabled) MySharpHotReload.Refresh();
    }
}
