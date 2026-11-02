using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Controls.Templates;
using MySharp;

AppBuilder.Configure<BenchmarkApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).WithInterFont().SetupWithoutStarting();
var results = new List<object>();
void Measure(string name, Action operation)
{
    var warmup = Stopwatch.StartNew();
    for (var i = 0; i < 500 || warmup.Elapsed.TotalSeconds < 2; i++) operation();
    var times = new List<double>();
    var allocations = new List<double>();
    for (var batch = 0; batch < 7; batch++)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var start = Stopwatch.GetTimestamp();
        for (var i = 0; i < 200; i++) operation();
        times.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds / 200);
        allocations.Add((GC.GetAllocatedBytesForCurrentThread() - allocated) / 200.0);
    }
    results.Add(new
    {
        name, median_ms = times.Order().ElementAt(3), max_batch_mean_ms = times.Max(),
        median_thread_allocated_bytes = allocations.Order().ElementAt(3), samples_ms = times
    });
}
foreach (var page in new[] { "overview", "tasks", "settings", "lab" })
{
    using var state = new Workspace(page);
    using var view = new UiView(state.Build);
    var window = new Window { Width = 1120, Height = 820, Content = view };
    window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
    Measure(page + ": unchanged declaration + layout", () =>
    {
        view.Rebuild(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
    });
    if (page == "overview")
    {
        var input = view.GetLogicalDescendants().OfType<TextBox>().Single();
        var alternate = false;
        Measure("overview: project filter change + layout", () =>
        {
            alternate = !alternate;
            input.Text = alternate ? "MySharp" : "";
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        });
    }
    window.Close();
}
var scale = new Dictionary<string, int>();
foreach (var virtualized in new[] { false, true })
{
    var items = Enumerable.Range(0, virtualized ? 10000 : 1000).ToArray();
    using var view = new UiView(cx =>
    {
        if (virtualized)
            Ui.Control<ListBox>(cx, () => new ListBox
            {
                ItemTemplate = new FuncDataTemplate<int>((item, _) => new TextBlock { Text = "Item " + item })
            }).Set<System.Collections.IEnumerable?>(ItemsControl.ItemsSourceProperty, items).Height(300);
        else
            Ui.Scroll(cx).Children(() => Ui.Column(cx).Children(() =>
            {
                foreach (var item in items) cx.Scope(item, () => Ui.Text(cx, "Item " + item));
            }));
    });
    var window = new Window { Width = 800, Height = 500, Content = view };
    window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
    var name = virtualized ? "native virtualized list: 10000 items" : "declared retained list: 1000 items";
    Measure(name, () => { view.Rebuild(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); });
    scale[name + ": realized ListBoxItem controls"] = view.GetVisualDescendants().OfType<ListBoxItem>().Count();
    window.Close();
}
var stableLabels = Enumerable.Range(0, 1000).Select(i => "Item " + i).ToArray();
using (var view = new UiView(cx => Ui.Column(cx).Fill().Children(stableLabels, static (ui, labels) =>
{
    for (var i = 0; i < labels.Length; i++)
        ui.Scope(i, labels[i], static (row, label) => Ui.Text(row, label).FontSize(12).Padding(1, 2));
})))
{
    var window = new Window { Width = 300, Height = 600, Content = view };
    window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
    Measure("MyGo-style fixture: 1000 precomputed labels with static callbacks", () =>
    {
        view.Rebuild(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
    });
    window.Close();
}
// Isolate framework resource lookup from application string formatting/captures.
{
    using var view = new UiView(cx =>
    {
        for (var i = 0; i < 1000; i++)
            cx.Scope(i, 0, static (row, _) => { _ = row.State(0).Value; });
    });
    var window = new Window { Width = 300, Height = 600, Content = view };
    window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
    if (view.StorageStats.Resources != 1000) throw new InvalidOperationException("State fixture must retain 1000 independent resources.");
    Measure("resource lookup: 1000 retained local states with static callbacks", () =>
    {
        view.Rebuild(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
    });
    window.Close();
}
{
    var items = new[] { "Original", "Translated", "Both" };
    using var view = new UiView(cx => Ui.Segmented(cx, items, 0, static _ => { }));
    var window = new Window { Width = 400, Height = 160, Content = view };
    window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
    Measure("segmented: unchanged declaration + layout", () =>
    {
        view.Rebuild(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
    });
    window.Close();
}
var output = JsonSerializer.Serialize(new
{
    environment = "Release; .NET 10; Avalonia.Headless + MySharpTheme + Inter; pages 1120x820, lists 800x500, static labels 300x600; at least 500 warmups over 2s then 7 batches of 200 operations",
    limitation = "Includes declaration/reconciliation, queued UI work and layout. Excludes native compositor, GPU rendering and real input latency. Allocations are UI-thread managed allocations, not retained memory.",
    results,
    scale
}, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine(output);
Directory.CreateDirectory("artifacts");
File.WriteAllText("artifacts/runtime-rebuild.json", output + "\n");

sealed class BenchmarkApp : Application
{
    public override void Initialize()
    {
        Styles.Add(MySharpTheme.CreateFluentBase());
        Styles.Add(new MySharpTheme());
    }
}
