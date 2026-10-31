# Rebuild profiling

## Baseline and evidence

The baseline was measured on the current caller-location, three-page application before retained-property changes. `artifacts/profile-baseline.json` records 7 batches of 200 operations after at least two seconds warmup. A dotnet-trace recording with dotnet-sampled-thread-time and gc-verbose is in `artifacts/baseline.nettrace`; the top-method summary is `artifacts/profile-baseline-stacks.txt`.

The sampled report includes TextBlock.CreateTextLayout, HarfBuzz shaping, property-store notifications, UiContext.Add/Sync and Brush.Parse. These sampled-thread-time percentages include background/waiting threads and are not CPU-usage percentages. Source inspection showed every declared node had its properties cleared before reapplying them, forcing otherwise identical fonts/colors through notifications and text-layout invalidation. Add also captured lambdas even for retained nodes, and Sync constructed sets on every traversal.

## First measured change

- Typed retained property slots compare native values; unchanged properties are not set again.
- Omitted modifiers clear their prior local value once, preserving conditional-style semantics.
- Immutable brushes are cached by color string; modifier methods avoid capturing Action closures.
- Create-only event closures are outside the retained-node hot path.
- Parent frame lists/sets/counters are reused. Unchanged child sequences return immediately from reconciliation.
- Existing retention, focus, selection, duplicate-ID and conditional-style tests still pass; an additional test asserts that an unchanged declaration raises no native property notifications.

| Operation | Baseline median | First optimized median | Baseline bytes/op | Optimized bytes/op |
| --- | ---: | ---: | ---: | ---: |
| Overview unchanged | 0.3795 ms | 0.0207 ms | 225,030 | 32,288 |
| Tasks unchanged | 0.3572 ms | 0.0198 ms | 228,257 | 30,168 |
| Settings unchanged | 0.2768 ms | 0.0139 ms | 156,903 | 20,592 |
| Project filter change | 0.5497 ms | 0.1977 ms | 418,881 | 248,870 |

The first optimized artifact is `artifacts/profile-retained-properties.json`. These numbers predate the state/task additions; the final combined API must be profiled again. They measure managed declaration, queue processing and layout on Headless + Fluent + Inter, not native composition, GPU drawing, scrolling FPS or end-to-end input latency.

## Reproduce

```bash
dotnet run --project benchmarks/MySharp.Benchmarks -c Release
# Optional diagnostic tool, not an application dependency:
dotnet tool install dotnet-trace --tool-path "$HOME/.local/share/mysharp-tools"
DOTNET_ROOT="$HOME/.dotnet" "$HOME/.local/share/mysharp-tools/dotnet-trace" collect \
  --profile dotnet-sampled-thread-time,gc-verbose --output artifacts/rebuild.nettrace \
  -- "$HOME/.dotnet/dotnet" benchmarks/MySharp.Benchmarks/bin/Release/net10.0/MySharp.Benchmarks.dll
```

Run untraced timing separately from tracing, sequentially. Tracing changes measurement overhead. `UiView.ProfilingEnabled` optionally records per-build UiFrameProfile and raises FrameMeasured: elapsed rebuild time, cumulative reconciliation time, UI-thread managed allocations, node count, created nodes and declarative Set/Clear property writes. It excludes subsequent native rendering; leave profiling disabled for normal timing comparisons.

## Remaining work

Changed-list filtering still recreates removed native controls. Application closures/formatting and full declaration traversal remain per-build costs. The later checkpoints below replace string paths and reference handles with numeric identities and value handles. Larger retained lists, real rendering and actual state/task interaction need coverage before making broad per-frame performance claims.


## MyGo-inspired final combined checkpoint

After the local MyGo review (see MYGO-OPTIMIZATIONS.md), numeric node/scope identities removed string path construction, Element became a 16-byte checked value handle, static state-passing callbacks were added, and sparse retained buffers gained shrink cleanup. No new source generator or rendering dependency was introduced.

`artifacts/profile-current-combined.json` is an earlier combined **untraced** checkpoint with state/task/window/native APIs present (see subsequent checkpoints below):

| Operation | Median | UI-thread bytes/op |
| --- | ---: | ---: |
| Overview unchanged | 0.01437 ms | 3,888 |
| Tasks unchanged | 0.01430 ms | 4,104 |
| Settings unchanged | 0.00979 ms | 2,672 |
| State/task lab unchanged | 0.00945 ms | 2,795 |
| Project filter change | 0.17218 ms | 116,508 |
| 1,000 rows with dynamic strings/captures | 0.17300 ms | 158,504 |
| Native virtualized ListBox, 10,000 items | 0.00061 ms | 40 |
| 1,000 precomputed labels/static callbacks | 0.16733 ms | 0 |

The virtualized fixture realizes 8 ListBoxItem containers, and its remaining 40 bytes include the fixture's own capture. It is not measuring 10,000 rendered rows. The precomputed-label fixture is a distinct case; the original allocating case was kept unchanged. Normal page allocations include application callbacks, strings and local-state declarations. There is no universal zero-allocation claim.

Viewport sizes are 1120×820 for pages, 800×500 for lists, 300×600 for the static label fixture. The .NET runtime, OS workload and scheduling affect small timings. These measurements exclude compositor/GPU drawing. A prior post-property-change trace (`retained.nettrace` / `profile-retained-stacks.txt`) identified string construction as a new visible hot path and motivated numeric identity; sampled waiting-thread percentages must not be interpreted as CPU usage.

### Owned-template checkpoint

`artifacts/profile-owned-templates.json` reruns the existing fixtures after adding the owned
native template API (the fixtures do not themselves use that new template). Overview/tasks/
settings/lab unchanged declaration + layout medians are respectively 0.013946 / 0.014152 /
0.009823 / 0.009332 ms; UI-thread allocations remain 3,888 / 4,104 / 2,672 / ~2,795 B.
The 1,000 precomputed labels/static callback fixture is 0.170474 ms and 0 B; the dynamic
labels/captures fixture is 0.175854 ms and 158,504 B. This repeat shows no material regression
in the existing fixtures; it is not a benchmark of template realization or native drawing.
The headless test suite now passes 60 tests. These additions have not yet been republished to AOT;
`current-aot` remains the prior checkpoint.

### Static resource factories and input/popup checkpoint

Resource cache hits no longer allocate framework factory closures for `State`, manual/keyed
`Task`, `Template` or owned `Flyout`: constructors take explicit arguments through cached
static delegates. User-created captures and dynamically formatted strings remain application
costs. A new fixture isolates 1,000 scoped retained local state lookups, verifies that 1,000
resources really exist, and performs declaration + queue processing + layout after warmup.

`artifacts/profile-state-factories.json` records that fixture at **0.132709 ms / 0 B** UI-thread
allocation per operation. The static 1,000-label fixture is 0.173155 ms / 0 B. Overview/tasks/
settings remain 0.014098 / 0.014299 / 0.009819 ms with unchanged allocations. The lab is
0.009931 ms / ~2,499 B, now including an owned flyout and a task hotkey; its UI differs from
the older lab fixture, so it is not a controlled before/after timing comparison. Other workers
paused heavy compilation/inference during this untraced measurement. Rendering/GPU and
whole-process/retained-memory costs remain outside these numbers.

The same source checkpoint publishes successfully as Native AOT in
`artifacts/publish/current-input-aot` (3 files, 33.7345 MiB including Skia/HarfBuzz).
The four-page plus custom-chrome startup smoke passes, and the popup/Escape/Ctrl+L screenshot script also
passes when pointed at this native executable via `MYSHARP_NATIVE_BINARY`. This updates
the AOT evidence for the new APIs; earlier `current-aot` output remains a historical checkpoint.
