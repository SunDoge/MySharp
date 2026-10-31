# State and async work

These APIs are implemented and under behavior tests. They are part of the active capability work, not a claim of completed Avalonia feature parity.

## Shared and local state

```csharp
using var selected = State.Create("overview"); // owned by the app/model

void Build(UiContext cx)
{
    var count = cx.State(0); // owned by this view/call site/Scope
    Ui.Text(cx, count.Value.ToString());
    Ui.Button(cx, "Increment").OnClick(() => count.Value++);
}
```

Reading `Value` during a build subscribes that view. Writes coalesce rebuild requests; equal values (using the supplied comparer or EqualityComparer<T>.Default) do not notify. Subscriptions no longer read on the next build are removed. Shared state is never disposed by a consuming view. `cx.State(() => CreateModel())` invokes the initializer only for the first declaration of that identity, or after unmount/remount.

`UiView` invalidations coalesce on any thread before posting to the UI dispatcher. A failed initial build disposes its resources and cancels already-queued declarations.

Local state follows caller location + parent/Scope identity, just like controls. Declare state inside `cx.Scope(item.Id, ...)` for dynamic instances. State not declared on a successful build is disposed. Using a removed local state handle throws ObjectDisposedException rather than silently mutating an orphan.

State getters and setters are UI-thread APIs. Use `await state.SetAsync(value)` or `await state.UpdateAsync(old => ...)` from a background task. Never synchronously wait for those tasks on the UI thread. Ordinary mutable objects inside a State still need an assignment or Update to notify; mutating a List in place is not automatically observed; use Mutate(action) for an explicit in-place notification. State disposal releases the container value but does not call Dispose on arbitrary payloads; use cx.Resource for owned IDisposable objects.

## Manual task

```csharp
var load = cx.Task<string>();
Ui.Button(cx, "Load").Disabled(load.IsRunning).OnClick(() =>
    load.Run(async token =>
    {
        await Task.Delay(500, token);
        return "Loaded";
    }));
Ui.Text(cx, load.Status.ToString());
if (load.HasValue) Ui.Text(cx, load.Result!);
if (load.Error is { } error) Ui.Text(cx, error.Message);
```

Run starts work on the thread pool, so even a synchronous prefix of the factory does not execute inside UI declaration. The factory must not access UI-thread state or controls. Capture immutable input values before Run; return results rather than mutating UI state in the worker. Status, Result and Error reads subscribe the view. Completion is a Task that finishes after the result is dispatched back to UI; await it, do not block the UI loop. Exceptions from work become Failed/Error state and are observed.

Cancel immediately publishes Canceled and requests token cancellation. Restart repeats the last Run factory. Restart/dependency replacement advances a version; results from earlier runs cannot overwrite the newest result, even when old work ignores cancellation. Cancellation remains cooperative: a factory that never completes cannot be forcibly stopped.

## Task keyed by a dependency

```csharp
var result = cx.Task(query, async (string query, CancellationToken token) =>
{
    await Task.Delay(250, token);
    return query.ToUpperInvariant();
});
```

This starts once per dependency value. Equal keys do not restart on rebuild. Changes cancel the previous run and clear the old result while the new run is pending. Use a tuple/record of all input dependencies; mutating a referenced object without changing equality is not a dependency change. A failed task is not retried automatically each build; call Restart to retry.

Both manual and keyed task handles are view/Scope resources. Unmount or view disposal cancels them and ignores subsequent results. Reappearing scopes get fresh task resources. Local State and tasks do not currently support persistence or state restoration across source edits.

## Tests

The executable headless test suite checks shared subscriptions, conditional dependency reads, scoped initialization/reorder/unmount, background state writes and dispatcher updates, success/error, no accidental restarts, changed dependency cancellation, stale completion, explicit cancel/restart and late results after unmount.


## UI-affine work and owned resources

`Run` uses the worker pool; `RunOnUi` invokes its asynchronous factory on the dispatcher so native clipboard, picker or dialog APIs can be called. Avoid blocking CPU work in that synchronous prefix. Restart retains the chosen execution mode. A native dialog API without a cancellation parameter may continue its UI operation after task cancellation; the handle still ignores its late result.

`cx.Resource(() => ownedDisposable)` initializes once per caller/Scope and disposes when omitted, unmounted or the initial view construction fails. It is appropriate for explicit resource ownership; arbitrary objects inside State are not automatically disposed.

Direct bindings are available for TextInput, Checkbox, Switch, Slider, NumberInput, DateInput, TimeInput, Select and List by passing State<T> in place of the value/callback pair. For high-volume pure rows, Children(state, static callback) and Scope(key, state, static callback) avoid closure allocation.

## Native collection templates

`cx.Template<T>` returns a retained Avalonia `IDataTemplate` for native collection controls:

```csharp
var template = cx.Template<Project>((row, project) =>
{
    Ui.Row(row).Children(() =>
    {
        Ui.Text(row, project.Name);
        var expanded = row.State(false);
        Ui.Checkbox(row, "Expanded", expanded);
    });
});
Ui.List(cx, projects, selected)
    .Set(ItemsControl.ItemTemplateProperty, template);
```

Each visually attached row has an independent identity root. Its local state, resources and tasks
are disposed when the row leaves the visual tree, including virtualization. Reattachment creates
a fresh local state. Keep persistent item state in the application model, keyed by its business ID.
The template itself is owned by the declaring scope, and releases all active rows on unmount.
Updating the parent supplies the latest row builder and schedules attached rows to refresh.
The helper does not implement recycling; native virtualization remains responsible for realization.
Null and wrong-type data do not match. For hierarchical tree templates, use Avalonia's native
`ITreeDataTemplate` through the existing property API until a dedicated owned helper is provided.

## Cleanup failures

If an owned resource or native control throws during disposal, MySharp still attempts the
remaining cleanup and reports an `AggregateException`. Unmounted entries are removed before
disposal and will not be retried accidentally. View disposal remains idempotent after a failure;
its content, owned resources and state subscriptions are released. If initial construction and
cleanup both fail, the reported aggregate preserves the original build exception as well.
A failed control-type replacement removes the disposed old identity; a later rebuild can retry.
This is cleanup robustness, not transactional rollback of a partially executed declaration.
