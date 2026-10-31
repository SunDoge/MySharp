# Design reference: local MyGo

[MyGo](https://github.com/egoist/mygo) was inspected read-only, specifically:

- `ui/frame.go`: checked value Element handles, owner detachment and generation retirement.
- `ui/context.go`: reusable storage, identity composition/key domains and cleanup of unused data.
- `ui/api_bench_test.go`: warmed precomputed-label fixture and visible-row list fixture.
- `ui/memory_test.go` and `docs/ui/performance.md`: shrinking/cleanup and explicit measurement boundaries.

The C# implementation uses Avalonia's controls/property store rather than copying MyGo's renderer or layout engine. The architecture ideas applied here are:

1. **Value handles, not per-declaration wrapper objects.** Element is a 16-byte readonly struct with an owner record, slot and generation. Resolve validates the active build; default handles are absent. Dispose detaches the owner, so a saved expired Element does not retain the view. Before generation rollover the owner is replaced, preventing an old handle from becoming valid again.
2. **Numeric identities.** Node/scope maps use 64-bit composition of parent ID, scope ID and caller/explicit key instead of allocating full string paths. Caller file/line/occurrence and typed key metadata are retained in an address struct; a hash collision with a different address throws instead of silently reusing a control. Key kinds have separate hash domains (string/number/GUID/explicit source).
3. **Reuse while warm, release when small.** Frames/lists/sets are reused. Unused nodes, subscriptions, task resources and scopes are removed. Large sparse buffers trim after a large view shrinks, with a conservative threshold to avoid normal list-growth churn. Removed-reference scratch buffers are cleared immediately.
4. **Offer static state-passing callbacks.** `Children(state, static (cx, state) => ...)` and `Scope(key, state, static (cx, state) => ...)` avoid per-item capture objects without requiring every normal user callback to be static.

```csharp
Ui.Column(cx).Children(labels, static (ui, labels) =>
{
    for (var i = 0; i < labels.Length; i++)
        ui.Scope(i, labels[i], static (row, label) =>
            Ui.Text(row, label).FontSize(12).Padding(1, 2));
});
```

The benchmark keeps the existing dynamic-string/captured-callback list fixture unchanged. A separate precomputed-label/static-callback fixture was added to make the authoring distinction explicit. The latter measured 0 B per steady declaration/layout operation after warmup; this is a property of that fixture, not a blanket zero-allocation claim for every application.

Artifacts:

- `profile-baseline.json`: original retained-control API before property optimization.
- `profile-retained-properties.json`: first property/frame change.
- `profile-value-handles.json`: value handles before numeric identity.
- `profile-numeric-identities.json`: numeric identity + static-callback fixture.

On this machine, the numeric-identity artifact records overview 0.017 ms / 3,952 B, task page 0.017 ms / 4,104 B, and settings 0.013 ms / 2,672 B per unchanged declaration/layout operation. The 1,000 precomputed labels fixture records 0.188 ms / 0 B. These predate a few subsequent cleanup/capability additions and should be rerun for final combined delivery.

MyGo's documented label timing includes CPU rendering in its Tester, on different hardware/OS. The C# fixture excludes compositor/GPU work. Do not report one as faster based on these numbers.

The subsequent `cx.Template<T>` helper applies the same explicit-lifetime principle to Avalonia
native data templates: only attached row views subscribe to state or own tasks. Avalonia still
controls realization and virtualization. This helper does not implement a new virtualizer.
