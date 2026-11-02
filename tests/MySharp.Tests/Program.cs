using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using MySharp;
using MySharp.Formatting;

AppBuilder.Configure<Application>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
var passed = 0;
void Test(string name, Action test)
{
    test();
    passed++;
    Console.WriteLine($"PASS {name}");
}
void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
T[] Controls<T>(UiView view) where T : Control => view.GetLogicalDescendants().OfType<T>().ToArray();
void Click(Button button)
{
    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Dispatcher.UIThread.RunJobs();
}

Test("inline Clicked consumes once and refreshes earlier text", () =>
{
    var count = 0;
    using var view = new UiView(cx =>
    {
        Ui.Column(cx).Children(() =>
        {
            Ui.Text(cx, count.ToString());
            if (Ui.Button(cx, "+").Clicked()) count++;
        });
    });
    var button = Controls<Button>(view).Single();
    Click(button);
    Assert(count == 1 && Controls<TextBlock>(view).Single().Text == "1", "Inline click must update earlier text.");
    Assert(ReferenceEquals(button, Controls<Button>(view).Single()), "Button must be retained.");
    view.Rebuild();
    Assert(count == 1, "Click must not repeat.");
});

Test("rapid clicks before a build are not dropped", () =>
{
    var count = 0;
    using var view = new UiView(cx => { if (Ui.Button(cx, "+").Clicked()) count++; });
    var button = Controls<Button>(view).Single();
    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Dispatcher.UIThread.RunJobs();
    Assert(count == 2, "Both queued clicks must be consumed.");
});

Test("OnClick runs exactly once with current closure", () =>
{
    var count = 0;
    var delta = 1;
    using var view = new UiView(cx =>
    {
        var step = delta;
        Ui.Button(cx, "add").OnClick(() => count += step);
    });
    delta = 5;
    view.Rebuild();
    Click(Controls<Button>(view).Single());
    Assert(count == 5, "Callback must use current render closure.");
});

Test("keyed reorder retains text inputs and selection", () =>
{
    var keys = new List<string> { "a", "b" };
    var values = new Dictionary<string, string> { ["a"] = "alpha", ["b"] = "beta" };
    using var view = new UiView(cx => Ui.Column(cx).Children(() =>
    {
        foreach (var key in keys) Ui.TextInput(cx, values[key], value => values[key] = value, key);
    }));
    var inputs = Controls<TextBox>(view);
    inputs[0].CaretIndex = 3;
    keys.Reverse();
    view.Rebuild();
    var reordered = Controls<TextBox>(view);
    Assert(ReferenceEquals(inputs[0], reordered[1]) && reordered[1].CaretIndex == 3, "Keyed input must survive reorder.");
    reordered[1].Text = "changed";
    Dispatcher.UIThread.RunJobs();
    Assert(values["a"] == "changed", "User text must update model.");
    Assert(ReferenceEquals(inputs[0], Controls<TextBox>(view)[1]), "Typing must preserve TextBox identity.");
});

Test("focused input remains focused across rebuild and keyed reorder", () =>
{
    var keys = new List<string> { "a", "b" };
    using var view = new UiView(cx => Ui.Column(cx).Children(() =>
    {
        foreach (var key in keys) Ui.TextInput(cx, key, _ => { }, key);
    }));
    var window = new Window { Content = view };
    try
    {
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var input = Controls<TextBox>(view)[0];
        Assert(input.Focus(), "Input must accept focus.");
        keys.Reverse();
        view.Rebuild();
        Assert(input.IsFocused, "Keyed reorder must preserve focus.");
    }
    finally { window.Close(); }
});

Test("conditional styles reset and omitted children disappear", () =>
{
    var styled = true;
    using var view = new UiView(cx =>
    {
        var column = Ui.Column(cx);
        if (styled) column.Background("#FF0000").Children(() => Ui.Text(cx, "old"));
    });
    styled = false;
    view.Rebuild();
    Assert(Controls<TextBlock>(view).Length == 0, "Omitted children must disappear.");
    Assert(Controls<Border>(view).First().Background is null, "Removed modifier must reset.");
});

Test("same key with new node kind replaces subtree safely", () =>
{
    var row = false;
    using var view = new UiView(cx =>
    {
        var layout = row ? Ui.Row(cx, "container") : Ui.Column(cx, "container");
        layout.Children(() => Ui.Text(cx, "child", "child"));
    });
    row = true;
    view.Rebuild();
    Assert(Controls<TextBlock>(view).Single().Text == "child", "Replacement subtree must attach safely.");
});

Test("duplicate keys and stale Elements fail clearly", () =>
{
    Element? stale = null;
    using var view = new UiView(cx => stale = Ui.Text(cx, "x"));
    var threw = false;
    try { stale!.Value.FontSize(20); } catch (InvalidOperationException) { threw = true; }
    Assert(threw, "Stale element must fail.");
    threw = false;
    try
    {
        using var invalid = new UiView(cx => { Ui.Text(cx, "a", "same"); Ui.Text(cx, "b", "same"); });
    }
    catch (InvalidOperationException) { threw = true; }
    Assert(threw, "Duplicate keys must fail.");
});

Test("Invalidate coalesces and disposed views ignore queued builds", () =>
{
    var builds = 0;
    var view = new UiView(cx => { builds++; Ui.Text(cx, "x"); });
    view.Invalidate(); view.Invalidate();
    Dispatcher.UIThread.RunJobs();
    Assert(builds == 2, "Invalidations must coalesce.");
    view.Invalidate(); view.Dispose();
    Dispatcher.UIThread.RunJobs();
    Assert(builds == 2, "Dispose must cancel queued builds.");
});

Test("formatter flattens chains, preserves ordinary methods and is idempotent", () =>
{
    var source = "class C { [UiComponent] void Build() {\n    Ui.Text(cx, \"a.b()\")\n        .FontSize(14)\n        .Bold();\n} void Ordinary() { x\n .A()\n .B(); } }";
    var formatted = CompactFormatter.Format(source);
    Assert(formatted.Contains("Ui.Text(cx, \"a.b()\").FontSize(14).Bold()"), "Chain should flatten.");
    Assert(formatted.Contains("x\n .A()\n .B()"), "Ordinary methods must remain untouched.");
    Assert(CompactFormatter.Format(formatted) == formatted, "Formatting must be idempotent.");
    var narrow = CompactFormatter.Format(source, 40);
    Assert(CompactFormatter.Format(narrow, 40) == narrow, "Wrapping must be idempotent.");
});

Test("formatter preserves comments, directives and multiline literals", () =>
{
    string[] cases =
    [
        "class C { [UiComponent] void B() { Ui.Text(cx, \"x\") /* keep */\n .Bold(); } }",
        "class C { [UiComponent] void B() { Ui.Text(cx, @\"a\nb\")\n .Bold(); } }",
        "class C { [UiComponent] void B() {\n#if DEBUG\nUi.Text(cx, \"x\").Bold();\n#endif\n} }"
    ];
    foreach (var source in cases)
        Assert(CompactFormatter.Format(source) == source, "Protected syntax must remain unchanged.");
    var rejected = false;
    try { CompactFormatter.Format("class {"); } catch (ArgumentException) { rejected = true; }
    Assert(rejected, "Invalid syntax must be rejected.");
});
Test("caller locations keep following input stable across conditional declarations", () =>
{
    var state = new IdentityFixture();
    using var view = new UiView(state.Build);
    var input = Controls<TextBox>(view).Single();
    input.CaretIndex = 2;
    state.Show = false;
    view.Rebuild();
    Assert(ReferenceEquals(input, Controls<TextBox>(view).Single()), "Call-site ID must survive preceding branch removal.");
    Assert(input.CaretIndex == 2, "Caret must survive conditional declaration changes.");
});

Test("caller component scopes distinguish helper invocations and dynamic instances", () =>
{
    var state = new IdentityFixture();
    using var view = new UiView(state.List);
    var before = Controls<TextBox>(view);
    Assert(before.Length == 4, "Two components and two list instances expected.");
    state.Items.Reverse();
    view.Rebuild();
    var after = Controls<TextBox>(view);
    Assert(ReferenceEquals(before[0], after[0]) && ReferenceEquals(before[1], after[1]), "Static component scopes must remain distinct.");
    Assert(ReferenceEquals(before[2], after[3]) && ReferenceEquals(before[3], after[2]), "Dynamic business scopes must retain instances.");
    state.Items.RemoveAt(0);
    view.Rebuild();
    Assert(Controls<TextBox>(view).Length == 3, "Removed scope must release its node.");
});

Test("caller identities are local to instance scopes and restore on exceptions", () =>
{
    var items = new List<string> { "a", "b" };
    using var view = new UiView(cx =>
    {
        foreach (var item in items) cx.Scope(item, () => Ui.TextInput(cx, item, _ => { }));
        try { cx.Scope("failed", () => throw new InvalidOperationException()); }
        catch (InvalidOperationException) { }
        Ui.Text(cx, "outside");
    });
    var before = Controls<TextBox>(view);
    items.Reverse(); view.Rebuild();
    Assert(ReferenceEquals(before[0], Controls<TextBox>(view)[1]), "Caller identities must be local to instance scope.");
    Assert(Controls<TextBlock>(view).Single().Text == "outside", "Scope must restore after exceptions.");
});

Test("repeating an explicit call site requires a unique instance scope", () =>
{
    var failed = false;
    try
    {
        using var view = new UiView(cx =>
        {
            for (var i = 0; i < 2; i++) Ui.Text(cx, "same", site: 123UL);
        });
    }
    catch (InvalidOperationException) { failed = true; }
    Assert(failed, "Duplicate call site must request scope.");
    failed = false;
    try
    {
        using var view = new UiView(cx =>
        {
            cx.Scope("same", () => Ui.Text(cx, "a"));
            cx.Scope("same", () => Ui.Text(cx, "b"));
        });
    }
    catch (InvalidOperationException) { failed = true; }
    Assert(failed, "Duplicate business scope must fail.");
});

Test("same-line widgets and automatic component scopes remain distinct", () =>
{
    using var view = new UiView(cx =>
    {
        Ui.TextInput(cx, "one", _ => { }); Ui.TextInput(cx, "two", _ => { });
        cx.Scope(() => Input(cx, "three")); cx.Scope(() => Input(cx, "four"));
    });
    static void Input(UiContext cx, string text) => Ui.TextInput(cx, text, _ => { });
    var before = Controls<TextBox>(view);
    Assert(before.Select(input => input.Text).SequenceEqual(new[] { "one", "two", "three", "four" }), "Same-line declarations must be distinct.");
    before[2].CaretIndex = 2;
    view.Rebuild();
    Assert(before.SequenceEqual(Controls<TextBox>(view)), "Same-line occurrence counts must reset each build.");
    Assert(before[2].CaretIndex == 2, "Scoped input must retain caret.");
});

Test("same-kind input after a conditional keeps caller identity", () =>
{
    var show = true;
    using var view = new UiView(cx =>
    {
        if (show) Ui.TextInput(cx, "optional", _ => { });
        Ui.TextInput(cx, "retained", _ => { });
    });
    var input = Controls<TextBox>(view)[1];
    show = false;
    view.Rebuild();
    Assert(ReferenceEquals(input, Controls<TextBox>(view).Single()), "Removing an earlier input on another line must not shift identity.");
});

Test("caller file path distinguishes equal line numbers and equal basenames", () =>
{
    using var view = new UiView(cx =>
    {
        Ui.TextInput(cx, "first", _ => { }, file: "/a/View.cs", line: 10);
        Ui.TextInput(cx, "second", _ => { }, file: "/b/View.cs", line: 10);
    });
    var before = Controls<TextBox>(view);
    view.Rebuild();
    Assert(before.Length == 2 && before.SequenceEqual(Controls<TextBox>(view)), "File identity must include its path.");
});

Test("explicit key overrides caller location when declaration moves between branches", () =>
{
    var first = true;
    using var view = new UiView(cx =>
    {
        if (first) Ui.TextInput(cx, "value", _ => { }, key: "stable");
        else Ui.TextInput(cx, "value", _ => { }, key: "stable");
    });
    var input = Controls<TextBox>(view).Single();
    first = false; view.Rebuild();
    Assert(ReferenceEquals(input, Controls<TextBox>(view).Single()), "An explicit key must override file and line.");
});

Test("caller component scopes survive unrelated conditional siblings", () =>
{
    var show = true;
    static void Input(UiContext cx) => Ui.TextInput(cx, "value", _ => { });
    using var view = new UiView(cx =>
    {
        if (show) cx.Scope(() => Input(cx));
        cx.Scope(() => Input(cx));
    });
    var input = Controls<TextBox>(view)[1];
    show = false; view.Rebuild();
    Assert(ReferenceEquals(input, Controls<TextBox>(view).Single()), "Automatic component scope must use caller location, not global order.");
});

Test("demo pages navigate, filter projects, add and complete tasks", () =>
{
    var workspace = new Workspace();
    using var view = new UiView(workspace.Build);
    Assert(Controls<TextBlock>(view).Any(t => t.Text == "Projects"), "Overview must render.");
    var search = Controls<TextBox>(view).Single();
    search.Text = "zz-no-match";
    Dispatcher.UIThread.RunJobs();
    Assert(Controls<TextBlock>(view).Any(t => t.Text == "No matching projects. Try another name."), "Project empty state expected.");
    Click(Controls<Button>(view).Single(b => Equals(b.Content, "Tasks")));
    var input = Controls<TextBox>(view).Single();
    input.Text = "Test a retained task";
    Dispatcher.UIThread.RunJobs();
    Click(Controls<Button>(view).Single(b => Equals(b.Content, "Add task")));
    Assert(Controls<CheckBox>(view).Any(t => Equals(t.Content, "Test a retained task")), "Added task must appear.");
    Controls<CheckBox>(view).First(b => b.IsChecked == false).IsChecked = true;
    Dispatcher.UIThread.RunJobs();
    Click(Controls<Button>(view).Single(b => Equals(b.Content, "Completed")));
    Assert(!Controls<CheckBox>(view).Any(t => Equals(t.Content, "Test a retained task")), "Open task must be filtered out.");
    Click(Controls<Button>(view).Single(b => Equals(b.Content, "Clear completed")));
    Assert(Controls<TextBlock>(view).Any(t => t.Text == "Nothing here yet. Add a task or choose another filter."), "Cleared list must show empty state.");
});

Test("settings commit profile values only on save and preserve state across navigation", () =>
{
    var workspace = new Workspace("settings");
    using var view = new UiView(workspace.Build);
    var inputs = Controls<TextBox>(view);
    inputs[0].Text = "Taylor";
    Dispatcher.UIThread.RunJobs();
    Assert(Controls<TextBlock>(view).Any(t => t.Text == "Alex Chen"), "Sidebar must keep saved profile until commit.");
    Controls<ToggleSwitch>(view).First().IsChecked = false;
    Dispatcher.UIThread.RunJobs();
    Click(Controls<Button>(view).Single(b => Equals(b.Content, "Save preferences")));
    Assert(Controls<TextBlock>(view).Any(t => t.Text == "Taylor"), "Saved name must update sidebar.");
    Assert(Controls<TextBlock>(view).Any(t => t.Text == "Preferences saved for this session."), "Save feedback must appear.");
    Click(Controls<Button>(view).Single(b => Equals(b.Content, "Overview")));
    Assert(!Controls<TextBlock>(view).Any(t => t.Text == "Recent activity"), "Saved activity preference must affect overview.");
    Click(Controls<Button>(view).Single(b => Equals(b.Content, "Settings")));
    Assert(Controls<TextBox>(view)[0].Text == "Taylor", "Profile state must survive page remount.");
});

Test("unchanged declarations do not reset or reapply native properties", () =>
{
    var bold = true;
    using var view = new UiView(cx =>
    {
        var text = Ui.Text(cx, "same").FontSize(20).TextColor("#123456");
        if (bold) text.Bold();
    });
    var native = Controls<TextBlock>(view).Single();
    var changes = 0;
    native.PropertyChanged += (_, _) => changes++;
    view.Rebuild();
    Assert(changes == 0, "An unchanged frame must not generate native property changes.");
    bold = false; view.Rebuild();
    Assert(native.FontWeight == Avalonia.Media.FontWeight.Normal, "Omitted bold must restore default.");
    Assert(changes > 0, "Removing a modifier must update the native property.");
});

Test("shared state automatically subscribes views and suppresses equal values", () =>
{
    using var state = State.Create(0);
    var firstBuilds = 0; var secondBuilds = 0;
    using var first = new UiView(cx => { firstBuilds++; Ui.Text(cx, state.Value.ToString()); });
    var second = new UiView(cx => { secondBuilds++; Ui.Text(cx, state.Value.ToString()); });
    state.Value = 0; Dispatcher.UIThread.RunJobs();
    Assert(firstBuilds == 1 && secondBuilds == 1, "Equal state must not invalidate.");
    state.Value = 1; Dispatcher.UIThread.RunJobs();
    Assert(firstBuilds == 2 && secondBuilds == 2, "All readers must update.");
    second.Dispose();
    state.Value = 2; Dispatcher.UIThread.RunJobs();
    Assert(firstBuilds == 3 && secondBuilds == 2, "Disposed views must unsubscribe.");
});

Test("state read subscriptions follow conditional declarations", () =>
{
    using var state = State.Create(0);
    var reads = true; var builds = 0;
    using var view = new UiView(cx => { builds++; Ui.Text(cx, reads ? state.Value.ToString() : "hidden"); });
    reads = false; view.Rebuild();
    state.Value = 5; Dispatcher.UIThread.RunJobs();
    Assert(builds == 2, "A state no longer read must not invalidate the view.");
    reads = true; view.Rebuild();
    state.Value = 6; Dispatcher.UIThread.RunJobs();
    Assert(builds == 4 && Controls<TextBlock>(view).Single().Text == "6", "Reading again must resubscribe.");
});

Test("scoped local state initializes once, follows business IDs, and disposes on unmount", () =>
{
    var keys = new List<string> { "a", "b" };
    var values = new Dictionary<string, State<int>>();
    var initialized = 0;
    using var view = new UiView(cx =>
    {
        foreach (var key in keys) cx.Scope(key, () =>
        {
            var value = cx.State(() => ++initialized);
            values[key] = value;
            Ui.Text(cx, value.Value.ToString());
        });
    });
    var a = values["a"]; var b = values["b"];
    a.Value = 10; Dispatcher.UIThread.RunJobs();
    keys.Reverse(); view.Rebuild();
    Assert(initialized == 2 && ReferenceEquals(a, values["a"]) && values["a"].Value == 10, "Scoped state must follow item identity.");
    keys.Remove("b"); view.Rebuild();
    var disposed = false;
    try { _ = b.Value; } catch (ObjectDisposedException) { disposed = true; }
    Assert(disposed, "Unmounted local state must dispose.");
    keys.Add("b"); view.Rebuild();
    Assert(initialized == 3 && !ReferenceEquals(b, values["b"]), "Remount must create fresh local state.");
});

void PumpUntil(Func<bool> complete)
{
    var timeout = System.Diagnostics.Stopwatch.StartNew();
    while (!complete() && timeout.Elapsed.TotalSeconds < 5)
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(1);
    }
    Dispatcher.UIThread.RunJobs();
    Assert(complete(), "Timed out waiting for dispatched async work.");
}

Test("state enforces UI-thread writes and offers async dispatcher updates", () =>
{
    using var state = State.Create(0);
    var error = Task.Run(() =>
    {
        try { state.Value = 1; return false; } catch (InvalidOperationException) { return true; }
    }).GetAwaiter().GetResult();
    Assert(error, "Background direct state writes must fail.");
    var update = Task.Run(() => state.SetAsync(2));
    PumpUntil(() => update.IsCompleted);
    update.GetAwaiter().GetResult();
    Assert(state.Value == 2, "SetAsync must dispatch the update.");
});

Test("manual tasks publish success and errors without blocking or rerunning on rebuild", () =>
{
    UiTask<int>? handle = null;
    var builds = 0;
    using var view = new UiView(cx =>
    {
        builds++;
        handle = cx.Task<int>();
        Ui.Text(cx, handle.Status.ToString());
    });
    var starts = 0;
    var result = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
    handle!.Run(_ => { Interlocked.Increment(ref starts); return result.Task; });
    Dispatcher.UIThread.RunJobs();
    Assert(handle.IsRunning && Controls<TextBlock>(view).Single().Text == "Running", "Pending task must render.");
    result.SetResult(42);
    PumpUntil(() => handle.Status == UiTaskStatus.Succeeded);
    Assert(handle.HasValue && handle.Result == 42 && starts == 1, "Successful task must run once.");
    view.Rebuild();
    Assert(starts == 1, "Rebuild must not restart manual work.");
    handle.Run(_ => Task.FromException<int>(new InvalidOperationException("expected")));
    PumpUntil(() => handle.Status == UiTaskStatus.Failed);
    Assert(handle.Error?.Message == "expected" && !handle.HasValue, "Task failures must be observable UI state.");
});

Test("keyed tasks cancel changed dependencies and ignore late stale results", () =>
{
    var dependency = "first";
    var calls = new System.Collections.Concurrent.ConcurrentDictionary<string, TaskCompletionSource<int>>();
    var tokens = new System.Collections.Concurrent.ConcurrentDictionary<string, CancellationToken>();
    UiTask<int>? handle = null;
    using var view = new UiView(cx =>
    {
        handle = cx.Task(dependency, (string key, CancellationToken token) =>
        {
            tokens[key] = token;
            return calls.GetOrAdd(key, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        });
        Ui.Text(cx, handle.Status.ToString());
    });
    PumpUntil(() => calls.ContainsKey("first"));
    var oldCompletion = handle!.Completion;
    view.Rebuild();
    Assert(calls.Count == 1, "Equal dependency must not restart work.");
    dependency = "second"; view.Rebuild();
    PumpUntil(() => calls.ContainsKey("second"));
    Assert(tokens["first"].IsCancellationRequested, "Dependency changes must cancel previous work.");
    calls["second"].SetResult(2);
    PumpUntil(() => handle.Status == UiTaskStatus.Succeeded);
    calls["first"].SetResult(1); // Deliberately ignores its cancellation token.
    PumpUntil(() => oldCompletion.IsCompleted);
    Assert(handle.Result == 2, "Late stale completion must not overwrite the current result.");
});

Test("cancel and restart remain deterministic even when old work ignores cancellation", () =>
{
    using var handle = new UiTask<int>();
    var first = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
    var runs = 0;
    handle.Run(_ => Interlocked.Increment(ref runs) == 1 ? first.Task : Task.FromResult(9));
    PumpUntil(() => Volatile.Read(ref runs) == 1);
    var old = handle.Completion;
    handle.Cancel();
    Assert(handle.Status == UiTaskStatus.Canceled, "Cancel must immediately publish canceled state.");
    handle.Restart();
    PumpUntil(() => handle.Status == UiTaskStatus.Succeeded);
    first.SetResult(1);
    PumpUntil(() => old.IsCompleted);
    Assert(handle.Result == 9 && runs == 2, "Restart must ignore the canceled run's result.");
});

Test("unmount cancels task resources and late completion cannot invalidate a disposed view", () =>
{
    var show = true;
    UiTask<int>? handle = null;
    var builds = 0;
    var source = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
    var tokenSeen = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
    var view = new UiView(cx =>
    {
        builds++;
        if (!show) return;
        handle = cx.Task<int>();
        Ui.Text(cx, handle.Status.ToString());
    });
    handle!.Run(token => { tokenSeen.SetResult(token); return source.Task; });
    PumpUntil(() => tokenSeen.Task.IsCompleted);
    var completion = handle.Completion;
    show = false; view.Rebuild(); Dispatcher.UIThread.RunJobs();
    Assert(tokenSeen.Task.Result.IsCancellationRequested, "Unmount must cancel task token.");
    var count = builds;
    view.Dispose();
    source.SetResult(3);
    PumpUntil(() => completion.IsCompleted);
    Assert(builds == count, "Late task result must not rebuild a disposed view.");
});

Test("native factory runs once, generic properties update and callbacks do not accumulate", () =>
{
    var created = 0; var value = 10.0; var notifications = 0;
    using var view = new UiView(cx => Ui.Control<Slider>(cx, () => { created++; return new Slider(); })
        .Set(Avalonia.Controls.Primitives.RangeBase.ValueProperty, value)
        .OnChanged(Avalonia.Controls.Primitives.RangeBase.ValueProperty, next => { value = next; notifications++; }));
    for (var i = 0; i < 10; i++) view.Rebuild();
    Assert(created == 1 && notifications == 0, "Factory/subscription must not repeat; declaration changes must not invoke input callback.");
    var slider = Controls<Slider>(view).Single();
    slider.Value = 25;
    Dispatcher.UIThread.RunJobs();
    Assert(value == 25 && notifications == 1, "Native input must publish once.");
    value = 40; view.Rebuild();
    Assert(slider.Value == 40 && notifications == 1, "Model updates must not loop through callback.");
});

Test("native handlers use latest closure, detach when omitted and ignore removed controls", () =>
{
    var listen = true; var show = true; var multiplier = 1; var total = 0;
    using var view = new UiView(cx =>
    {
        if (!show) return;
        var step = multiplier;
        var button = Ui.Control<Button>(cx).Set<object?>(ContentControl.ContentProperty, "native");
        if (listen) button.On(Button.ClickEvent, (_, _) => total += step);
    });
    var native = Controls<Button>(view).Single();
    multiplier = 3; view.Rebuild(); Click(native);
    Assert(total == 3, "Native event must use current closure.");
    listen = false; view.Rebuild(); Click(native);
    Assert(total == 3, "Omitted native handler must detach.");
    listen = true; view.Rebuild(); show = false; view.Rebuild(); Click(native);
    Assert(total == 3, "Removed native control must not invoke a stale handler.");
});

Test("convenience controls synchronize native inputs and retain identity", () =>
{
    var enabled = false; var selected = 0;
    using var view = new UiView(cx => Ui.Column(cx).Children(() =>
    {
        Ui.Checkbox(cx, "Enabled", enabled, value => enabled = value);
        Ui.Select(cx, new[] { "one", "two" }, selected, value => selected = value);
        Ui.Progress(cx, 50);
    }));
    var checkbox = Controls<CheckBox>(view).Single();
    checkbox.IsChecked = true;
    Dispatcher.UIThread.RunJobs();
    var combo = Controls<ComboBox>(view).Single();
    combo.SelectedIndex = 1;
    Dispatcher.UIThread.RunJobs();
    Assert(enabled && selected == 1, "Convenience control changes must update the model.");
    Assert(ReferenceEquals(checkbox, Controls<CheckBox>(view).Single()), "Native checkbox must be retained.");
    Assert(Controls<ProgressBar>(view).Single().Value == 50, "Progress value must map to Avalonia.");
});

Test("opt-in frame profiling reports retained nodes and zero unchanged property writes", () =>
{
    using var view = new UiView(cx => Ui.Text(cx, "profile").FontSize(18));
    view.ProfilingEnabled = true;
    var measured = 0;
    view.FrameMeasured += _ => measured++;
    view.Rebuild();
    Assert(measured == 1 && view.LastProfile.Nodes == 1 && view.LastProfile.CreatedNodes == 0,
        "Profile must describe the retained frame.");
    Assert(view.LastProfile.PropertyWrites == 0 && view.LastProfile.RebuildTime >= TimeSpan.Zero,
        "Unchanged declarative properties must not be written.");
});

Test("window options configure chrome and native escape hatch", () =>
{
    var window = MySharpApp.CreateWindow(cx => Ui.Text(cx, "window"), new WindowOptions
    {
        Chrome = WindowChrome.Custom, Width = 800, Height = 600, CanResize = false,
        Topmost = true, ShowInTaskbar = false, ConfigureWindow = native => native.Title = "configured"
    });
    Assert(window.WindowDecorations == WindowDecorations.None && window.ExtendClientAreaToDecorationsHint,
        "Custom chrome must extend into undecorated client area.");
    Assert(!window.CanResize && window.Topmost && !window.ShowInTaskbar && window.Title == "configured",
        "Window options and native configuration must apply.");
    window.Close();
});

Test("custom header roles and window controls map to native window state", () =>
{
    UiWindow? controller = null;
    var window = MySharpApp.CreateWindow(cx =>
    {
        controller = cx.Window;
        Ui.Row(cx).TitleBar().Children(() =>
        {
            Ui.Text(cx, cx.Window.State.ToString());
            Ui.WindowControls(cx);
        });
    }, new WindowOptions { Chrome = WindowChrome.Custom });
    window.Show(); Dispatcher.UIThread.RunJobs();
    var view = (UiView)window.Content!;
    Assert(ReferenceEquals(controller!.Native, window), "Controller must resolve attached window.");
    Assert(Controls<Border>(view).Any(border => Avalonia.Controls.Chrome.WindowDecorationProperties.GetElementRole(border) == Avalonia.Input.WindowDecorationsElementRole.TitleBar),
        "Header must carry the native drag role.");
    controller.Maximize(); Dispatcher.UIThread.RunJobs();
    Assert(window.WindowState == WindowState.Maximized && Controls<TextBlock>(view).Any(t => t.Text == "Maximized"), "Window state must notify the view.");
    controller.Restore();
    Assert(window.WindowState == WindowState.Normal, "Restore must reset window state.");
    window.Close();
});

Test("closing cancellation keeps view resources alive until actual window close", () =>
{
    var cancel = true;
    State<int>? state = null;
    var window = MySharpApp.CreateWindow(cx => { state = cx.State(1); Ui.Text(cx, state.Value.ToString()); },
        new WindowOptions { Closing = args => args.Cancel = cancel });
    window.Show(); Dispatcher.UIThread.RunJobs();
    window.Close();
    Assert(window.IsVisible && state!.Value == 1, "Canceled close must retain the live view.");
    cancel = false; window.Close();
    var released = false;
    try { _ = state!.Value; } catch (ObjectDisposedException) { released = true; }
    Assert(released, "Real close must dispose scoped state.");
});

Test("owned modal windows return typed results", () =>
{
    UiWindow? controller = null;
    var owner = MySharpApp.CreateWindow(cx => { controller = cx.Window; Ui.Text(cx, "owner"); });
    owner.Show();
    var dialog = MySharpApp.CreateWindow(cx => Ui.Text(cx, "dialog"), new WindowOptions { Title = "dialog" });
    var result = controller!.ShowDialog<int>(dialog);
    dialog.Close(42);
    PumpUntil(() => result.IsCompleted);
    Assert(result.Result == 42, "Modal result must flow through typed native API.");
    owner.Close();
});

Test("state and task lab cancels work on navigation and retains shared model state", () =>
{
    using var workspace = new Workspace("lab");
    using var view = new UiView(workspace.Build);
    Click(Controls<Button>(view).Single(b => Equals(b.Content, "Local count: 0")));
    Click(Controls<Button>(view).Single(b => Equals(b.Content, "Shared count: 0")));
    Click(Controls<Button>(view).Single(b => Equals(b.Content, "Load sample")));
    Assert(Controls<Button>(view).Single(b => Equals(b.Content, "Cancel")).IsEffectivelyEnabled, "Pending task should enable cancel.");
    Click(Controls<Button>(view).Single(b => Equals(b.Content, "Cancel")));
    Assert(Controls<TextBlock>(view).Any(t => t.Text == "Canceled"), "Canceled state should be visible.");
    Click(Controls<Button>(view).Single(b => Equals(b.Content, "Overview")));
    Click(Controls<Button>(view).Single(b => Equals(b.Content, "State & tasks")));
    Assert(Controls<Button>(view).Any(b => Equals(b.Content, "Local count: 0")), "Local state must reset on page remount.");
    Assert(Controls<Button>(view).Any(b => Equals(b.Content, "Shared count: 1")), "Shared state must survive navigation.");
});

Test("grid cells, spans and row definitions reset when modifiers disappear", () =>
{
    var explicitCell = true;
    using var view = new UiView(cx =>
    {
        var grid = Ui.Grid(cx, "*,*");
        if (explicitCell) grid.Rows("40,*");
        grid.Children(() =>
        {
            Ui.Text(cx, "first");
            var second = Ui.Text(cx, "second");
            if (explicitCell) second.Cell(1, 0, columnSpan: 2);
        });
    });
    var text = Controls<TextBlock>(view).Single(t => t.Text == "second");
    var box = (Border)text.Parent!;
    Assert(Grid.GetRow(box) == 1 && Grid.GetColumnSpan(box) == 2, "Explicit cell/span must apply.");
    explicitCell = false; view.Rebuild();
    Assert(Grid.GetRow(box) == 0 && Grid.GetColumn(box) == 1 && Grid.GetColumnSpan(box) == 1,
        "Omitted cell must restore normal flow without resetting the control.");
    Assert(Controls<Grid>(view).Last().RowDefinitions.Count == 0, "Omitted Rows must restore default definitions.");
});

Test("classes and primary style update without sticking or duplicating", () =>
{
    var active = true;
    using var view = new UiView(cx =>
    {
        var button = Ui.Button(cx, "class");
        if (active) button.Primary().Class("custom");
    });
    var native = Controls<Button>(view).Single();
    view.Rebuild();
    Assert(native.Classes.Count(name => name == "accent") == 1, "Primary must not duplicate classes.");
    active = false; view.Rebuild();
    Assert(!native.Classes.Contains("accent") && !native.Classes.Contains("custom"), "Omitted classes must clear.");
});

Test("hiding and showing a window keeps its controller usable", () =>
{
    UiWindow? controller = null;
    var window = MySharpApp.CreateWindow(cx => { controller = cx.Window; Ui.Text(cx, "visibility"); });
    window.Show(); Dispatcher.UIThread.RunJobs();
    controller!.Hide();
    controller.Show();
    Assert(window.IsVisible, "Controller must retain a hidden window for Show.");
    window.Close();
});

Test("UI-affine tasks execute their factory on the dispatcher and preserve mode on restart", () =>
{
    using var handle = new UiTask<int>();
    var dispatcherThread = Environment.CurrentManagedThreadId;
    var starts = new List<int>();
    handle.RunOnUi(async _ =>
    {
        starts.Add(Environment.CurrentManagedThreadId);
        await Task.Delay(1);
        return 7;
    });
    PumpUntil(() => handle.Status == UiTaskStatus.Succeeded);
    handle.Restart();
    PumpUntil(() => handle.Status == UiTaskStatus.Succeeded);
    Assert(starts.Count == 2 && starts.All(id => id == dispatcherThread), "RunOnUi and restart must preserve UI affinity.");
});

Test("mounted context exposes native top-level services", () =>
{
    UiContext? context = null;
    var window = MySharpApp.CreateWindow(cx => { context = cx; Ui.Text(cx, "services"); });
    window.Show(); Dispatcher.UIThread.RunJobs();
    Assert(ReferenceEquals(context!.TopLevel, window) && ReferenceEquals(context.Storage, window.StorageProvider),
        "Native services must come from the containing TopLevel.");
    Assert(ReferenceEquals(context.Clipboard, window.Clipboard) && ReferenceEquals(context.Screens, window.Screens),
        "Clipboard/screens must be directly accessible without replacing their native contract.");
    window.Close();
});

Test("native content controls retain child declarations across content panel replacement", () =>
{
    var replace = false;
    var panel = new Grid();
    using var view = new UiView(cx =>
    {
        var element = Ui.Control<Expander>(cx).Set(Expander.IsExpandedProperty, true);
        if (replace) element.Configure<Expander>(native => native.Content = panel);
        element.Children(() => Ui.TextInput(cx, "content", _ => { }));
    });
    var input = Controls<TextBox>(view).Single();
    replace = true; view.Rebuild();
    Assert(ReferenceEquals(input, Controls<TextBox>(view).Single()), "Reparenting a content panel must retain its declared input.");
});

Test("mutable state and direct state input bindings notify without manual callbacks", () =>
{
    using var items = State.Create(new List<int>());
    using var text = State.Create("initial");
    using var view = new UiView(cx =>
    {
        Ui.Text(cx, items.Value.Count.ToString());
        Ui.TextInput(cx, text);
    });
    items.Mutate(list => list.Add(1));
    Dispatcher.UIThread.RunJobs();
    Assert(Controls<TextBlock>(view).Single().Text == "1", "Mutate must notify even when collection reference is unchanged.");
    Controls<TextBox>(view).Single().Text = "edited";
    Dispatcher.UIThread.RunJobs();
    Assert(text.Value == "edited", "State overload must provide two-way text input.");
});

Test("owned resource factories run once and dispose on scope unmount", () =>
{
    var show = true; var created = 0; var disposed = 0;
    using var view = new UiView(cx =>
    {
        if (show) cx.Resource(() => { created++; return new Cleanup(() => disposed++); });
        Ui.Text(cx, "resource");
    });
    view.Rebuild();
    Assert(created == 1 && disposed == 0, "Retained resource must not recreate on rebuild.");
    show = false; view.Rebuild();
    Assert(disposed == 1, "Unmount must dispose the owned resource exactly once.");
});

Test("value element handles validate owner, build generation and default absence", () =>
{
    Assert(typeof(Element).IsValueType && System.Runtime.CompilerServices.Unsafe.SizeOf<Element>() == 16,
        "Element should be a 16-byte value handle rather than a per-node allocation.");
    Assert(!default(Element).IsValid, "Default element must be absent.");
    Element saved = default;
    using var view = new UiView(cx => { saved = Ui.Text(cx, "value"); Assert(saved.IsValid, "Handle must be valid in its build."); });
    Assert(!saved.IsValid, "Build-local handle must expire after the declaration ends.");
    var failed = false;
    try { default(Element).FontSize(20); } catch (InvalidOperationException) { failed = true; }
    Assert(failed, "Default handle use must produce a clear error.");
});

Test("generation rollover retires owners rather than reviving saved handles", () =>
{
    UiContext? context = null;
    Element current = default;
    Element stale = default;
    var check = false;
    using var view = new UiView(cx =>
    {
        context = cx;
        current = Ui.Text(cx, "generation");
        if (!check) return;
        Assert(!stale.IsValid, "Old owner must stay invalid when its generation number is reused.");
        var failed = false;
        try { stale.FontSize(20); } catch (InvalidOperationException) { failed = true; }
        Assert(failed, "Retired owner must refuse stale handles.");
    });
    stale = current;
    context!.Generation = int.MaxValue;
    check = true;
    view.Rebuild();
});

Test("saved expired value handles do not retain a disposed view", () =>
{
    var (element, weak) = SavedHandleFixture.Create();
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    Assert(!weak.IsAlive, "A saved expired handle must only retain a detached owner record.");
    GC.KeepAlive(element);
});

Test("numeric scope identities separate key types and preserve static callback state", () =>
{
    var values = new[] { (Id: 1L, Text: "one"), (Id: 2L, Text: "two") };
    using var view = new UiView(cx => Ui.Column(cx).Children(values, static (ui, items) =>
    {
        foreach (var item in items) ui.Scope(item.Id, item.Text, static (row, text) => Ui.TextInput(row, text, _ => { }));
        ui.Scope("1", "string key", static (row, text) => Ui.TextInput(row, text, _ => { }));
    }));
    var before = Controls<TextBox>(view);
    Array.Reverse(values); view.Rebuild();
    var after = Controls<TextBox>(view);
    Assert(ReferenceEquals(before[0], after[1]) && ReferenceEquals(before[2], after[2]), "Numeric and string scopes must not alias and typed callbacks must retain nodes.");
});

Test("failed initial declarations release scoped resources", () =>
{
    var disposed = 0;
    try
    {
        using var view = new UiView(cx =>
        {
            cx.Resource(() => new Cleanup(() => disposed++));
            throw new InvalidOperationException("expected");
        });
    }
    catch (InvalidOperationException) { }
    Assert(disposed == 1, "Constructor failure must release initialized resources.");
});

Test("failed first build cancels already queued state invalidations", () =>
{
    using var state = State.Create(0);
    var builds = 0;
    try
    {
        using var view = new UiView(cx =>
        {
            builds++;
            Ui.Text(cx, state.Value.ToString());
            state.Value++;
            throw new InvalidOperationException("expected");
        });
    }
    catch (InvalidOperationException) { }
    Dispatcher.UIThread.RunJobs();
    state.Value++;
    Dispatcher.UIThread.RunJobs();
    Assert(builds == 1, "Failed initial view must not execute a queued build or stay subscribed.");
});

Test("background invalidations coalesce before entering the UI dispatcher", () =>
{
    var builds = 0;
    using var view = new UiView(cx => { builds++; Ui.Text(cx, "background"); });
    Task.Run(() => { for (var i = 0; i < 1000; i++) view.Invalidate(); }).GetAwaiter().GetResult();
    Dispatcher.UIThread.RunJobs();
    Assert(builds == 2, "Concurrent requests must schedule one rebuild.");
});

Test("large views release sparse retained buffers after shrinking", () =>
{
    var count = 5000;
    using var view = new UiView(cx => Ui.Column(cx).Children(count, static (ui, count) =>
    {
        for (var i = 0; i < count; i++) ui.Scope(i, 0, static (row, _) => Ui.Text(row, "row"));
    }));
    var before = view.StorageStats;
    count = 4; view.Rebuild();
    var after = view.StorageStats;
    Assert(after.Nodes == 5 && after.Scopes == 4, "Removed rows/scopes must not remain in retained dictionaries.");
    Assert(after.NodeCapacity < before.NodeCapacity && after.HandleCapacity < before.HandleCapacity && after.ScopeCapacity < before.ScopeCapacity,
        "Large sparse node, slot and scope buffers must shrink after peak load.");
});

Test("direct native controls preserve parent-sensitive layout contracts", () =>
{
    using var view = new UiView(cx => Ui.Grid(cx, "*,8,*").Children(() =>
    {
        Ui.Text(cx, "left").Cell(0, 0);
        Ui.Direct<GridSplitter>(cx).Cell(0, 1).Width(8);
        Ui.Text(cx, "right").Cell(0, 2);
    }));
    var splitter = Controls<GridSplitter>(view).Single();
    Assert(splitter.Parent is Grid && Grid.GetColumn(splitter) == 1, "GridSplitter must be a direct child of its owning Grid.");
    view.Rebuild();
    Assert(ReferenceEquals(splitter, Controls<GridSplitter>(view).Single()), "Direct controls must still retain identity.");
});

Test("outer attached properties preserve Canvas layout despite decorating wrappers", () =>
{
    using var view = new UiView(cx => Ui.Control<Canvas>(cx).Children(() =>
        Ui.Text(cx, "positioned").SetOuter(Canvas.LeftProperty, 20.0).SetOuter(Canvas.TopProperty, 30.0)));
    var text = Controls<TextBlock>(view).Single();
    var outer = (Control)text.Parent!;
    Assert(Canvas.GetLeft(outer) == 20 && Canvas.GetTop(outer) == 30, "Attached layout properties must apply to the actual Canvas child.");
});

Test("GUID and explicit site scopes use different identity domains", () =>
{
    static void Input(UiContext cx, string value) => Ui.TextInput(cx, value, _ => { });
    using var view = new UiView(cx =>
    {
        cx.Scope(new CallSiteId(1), () => Input(cx, "site"));
        cx.Scope(new Guid("00000001-0000-0000-0000-000000000000"), () => Input(cx, "guid"));
    });
    var before = Controls<TextBox>(view);
    view.Rebuild();
    Assert(before.Length == 2 && before.SequenceEqual(Controls<TextBox>(view)), "Key-type domains must not produce deterministic aliases.");
});

Test("owned templates dispose detached rows and refresh captured data", () =>
{
    UiDataTemplate<int>? template = null;
    var cleanups = 0;
    var prefix = "first";
    var rowBuilds = 0;
    using var shared = State.Create(0);
    using var owner = new UiView(cx =>
    {
        var snapshot = prefix;
        template = cx.Template<int>((row, item) =>
        {
            rowBuilds++;
            _ = shared.Value;
            row.Resource(() => new Cleanup(() => cleanups++));
            Ui.Text(row, $"{snapshot}:{item}");
        });
    });
    var first = template;
    var rowControl = template!.Build(7)!;
    Assert(cleanups == 0, "Unrealized rows must not create resources.");
    var window = new Window { Content = rowControl };
    window.Show();
    Assert(rowControl.GetLogicalDescendants().OfType<TextBlock>().Single().Text == "first:7", "Attached row builds.");
    prefix = "second";
    owner.Rebuild(); Dispatcher.UIThread.RunJobs();
    Assert(ReferenceEquals(first, template), "Template must retain its identity.");
    Assert(rowControl.GetLogicalDescendants().OfType<TextBlock>().Single().Text == "second:7", "Attached rows receive latest builder.");
    window.Content = null;
    Assert(cleanups == 1, "Detach releases row resources.");
    var detachedBuilds = rowBuilds;
    shared.Value++; Dispatcher.UIThread.RunJobs();
    Assert(rowBuilds == detachedBuilds, "Detached rows unsubscribe shared state.");
    window.Content = rowControl;
    Assert(cleanups == 1, "Reattach starts a new row lifetime.");
    owner.Dispose();
    Assert(cleanups == 2 && !rowControl.GetLogicalDescendants().OfType<UiView>().Any(), "Template owner releases active rows.");
    window.Close();
});

Test("view cleanup aggregates failures and releases every owned resource", () =>
{
    var disposed = 0;
    using var shared = State.Create(0);
    var builds = 0;
    var view = new UiView(cx =>
    {
        builds++; _ = shared.Value;
        cx.Resource(() => new Cleanup(() => { disposed++; throw new InvalidOperationException("first cleanup"); }), key: "first");
        cx.Resource(() => new Cleanup(() => disposed++), key: "second");
        Ui.Control<ThrowingControl>(cx);
    });
    AggregateException? failure = null;
    try { view.Dispose(); } catch (AggregateException error) { failure = error.Flatten(); }
    Assert(failure?.InnerExceptions.Count == 2 && disposed == 2, "All resources and native controls must be cleaned despite errors.");
    Assert(view.Content is null && view.StorageStats.Nodes == 0 && view.StorageStats.Resources == 0, "Failed cleanup must release retained storage.");
    view.Dispose();
    shared.Value++; Dispatcher.UIThread.RunJobs();
    Assert(builds == 1, "Closed views must unsubscribe even when disposal throws.");
});

Test("unmount cleanup failures do not leave stale resources or block subsequent builds", () =>
{
    var mounted = true;
    var disposed = 0;
    using var view = new UiView(cx =>
    {
        Ui.Text(cx, "alive");
        if (!mounted) return;
        cx.Resource(() => new Cleanup(() => { disposed++; throw new InvalidOperationException("unmount"); }), key: "bad");
        cx.Resource(() => new Cleanup(() => disposed++), key: "good");
        Ui.Control<ThrowingControl>(cx, key: "bad-control");
    });
    mounted = false;
    AggregateException? failure = null;
    try { view.Rebuild(); } catch (AggregateException error) { failure = error.Flatten(); }
    Assert(failure?.InnerExceptions.Count == 2 && disposed == 2, "Node and resource sweeps must both finish.");
    Assert(view.StorageStats.Nodes == 1 && view.StorageStats.Resources == 0, "Removed entries cannot survive failed cleanup.");
    view.Rebuild();
    Assert(disposed == 2, "Cleanup must not repeat.");
});

Test("initial build preserves original failure alongside cleanup errors", () =>
{
    var disposed = 0;
    AggregateException? failure = null;
    try
    {
        _ = new UiView(cx =>
        {
            cx.Resource(() => new Cleanup(() => { disposed++; throw new Exception("cleanup"); }), key: "bad");
            cx.Resource(() => new Cleanup(() => disposed++), key: "good");
            throw new InvalidOperationException("original build");
        });
    }
    catch (AggregateException error) { failure = error.Flatten(); }
    Assert(disposed == 2 && failure?.InnerExceptions.Any(e => e.Message == "original build") == true,
        "Initial failure must remain diagnosable and all resources must release.");
});

Test("failed replacement cleanup retires the old control and allows retry", () =>
{
    var replace = false;
    using var view = new UiView(cx =>
    {
        if (replace) Ui.Text(cx, "new", key: "same");
        else Ui.Control<ThrowingControl>(cx, key: "same");
    });
    replace = true;
    try { view.Rebuild(); } catch (AggregateException) { }
    Assert(view.StorageStats.Nodes == 0, "Failed replacement must retire the disposed old node.");
    view.Rebuild();
    Assert(Controls<TextBlock>(view).Single().Text == "new", "Replacement should recover on the next build.");
});

passed += PopupInputTests.Run();
passed += StyleTests.Run();
passed += AnimationTests.Run();
passed += ThemeTests.Run();
passed += WorkspaceInputTests.Run();
passed += IconTests.Run();
passed += SegmentedTests.Run();
passed += ExpanderTests.Run();
passed += CollectionTests.Run();
passed += Game2048Tests.Run();
passed += PulseTests.Run();
passed += I18nTests.Run();
passed += HotReloadTests.Run();
Console.WriteLine($"{passed} tests passed.");

public sealed class IdentityFixture
{
    public bool Show = true;
    public List<string> Items = ["a", "b"];

    [Component]
    public void Build(UiContext cx)
    {
        if (Show) Ui.Text(cx, "optional");
        Ui.TextInput(cx, "value", _ => { });
    }

    [Component]
    public void Input(UiContext cx, string value)
    {
        Ui.TextInput(cx, value, _ => { });
    }

    [Component]
    public void List(UiContext cx)
    {
        cx.Scope(() => Input(cx, "left"));
        cx.Scope(() => Input(cx, "right"));
        cx.Scope(() =>
        {
            foreach (var item in Items) cx.Scope(item, () => Input(cx, item));
        });
    }
}

sealed class Cleanup(Action cleanup) : IDisposable { public void Dispose() => cleanup(); }

static class SavedHandleFixture
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static (Element, WeakReference) Create()
    {
        Element element = default;
        var view = new UiView(cx => element = Ui.Text(cx, "saved"));
        var weak = new WeakReference(view);
        view.Dispose();
        return (element, weak);
    }
}

sealed class ThrowingControl : Control, IDisposable
{
    public void Dispose() => throw new InvalidOperationException("native cleanup");
}
