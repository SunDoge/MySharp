# Best-effort .NET Hot Reload

MySharp has an optional `MySharp.HotReload` development helper. It uses .NET's
`MetadataUpdateHandler` notification to re-run declarations after a supported C# edit.
It does not replace .NET's compiler/runtime update mechanism and does not use interceptors,
source generators, source rewriting, structural ID matching, timers or file watchers inside the UI.

## Use

The Workspace sample and RawMangaReader2 enable the helper in Debug builds. Start either with:

```sh
dotnet watch --project MySharp.csproj run
# From the OCR application directory:
dotnet watch --project src/RawMangaReader.Desktop run
```

For another source consumer, reference the helper only in the development configuration:

```xml
<ProjectReference Include="path/to/MySharp.HotReload.csproj"
                  Condition="'$(Configuration)' == 'Debug'" />
```

Enable it before the application's main loop:

```csharp
#if DEBUG
MySharpHotReload.Enable();
#endif
MySharpApp.Run(Build);
```

`Enable()` returns false when `MetadataUpdater.IsSupported` is false, such as a normal run
without a Hot Reload-capable session. The helper assembly must be loaded (by this call)
for its update handler to participate. Release-built helper packages can also be referenced
by Debug applications; the conditional reference controls whether they ship with the application.

`MySharpHotReload.Refresh(window)` is an explicit dispatcher-safe refresh for custom window
hosts. The automatic `Refresh()` path enumerates the classic desktop lifetime's open windows.
No view registry or long-lived subscriptions are installed. Update notifications coalesce before
dispatch; active attached child/template views are included. Hidden/closed windows and detached
old child views are skipped. Popups in separate visual roots and non-desktop lifetimes are not
automatically discovered; custom hosts can explicitly refresh their windows.

## State and errors

Existing controls and local state are retained when their normal identity remains unchanged.
Changing text, spacing or a method body without moving call sites is the main supported use case.
The native X11 test changes text while preserving user input, a local counter and both original
input/button objects in the same process.

Identity still uses file/line/parent/Scope. Inserting lines, moving declarations or changing the
structure may remount controls and reset local State/Task/Resource values. Initializers/constructors
are not automatically rerun on already-existing objects. There is no promise of state migration
across arbitrary edits. Keep important document/business data in independently owned model objects.

Unsupported edits follow .NET/IDE/dotnet-watch behavior and may require a restart. Compiling a
bad edit does not apply it. If applied code throws during declaration, the helper records
`LastError`, writes a Trace error, continues refreshing other views, and permits a later valid
update to recover. A failed build is not transactional: partially changed controls may remain;
the helper does not clone/rollback the whole UI.

Native AOT/trimmed published applications are outside this development workflow. Use ordinary
Debug/JIT development, then publish normally. The helper is not a runtime plugin/reload engine.

## Performance contract and evidence

Neither `src/MySharp` nor `src/MySharp.Desktop` was modified. There are no added branches,
allocations, reflection lookups or identity changes in `Rebuild`, reconciliation, input or drawing.
Only metadata updates explicitly invoke window traversal. Ordinary idle operation has no helper
work, and the supplied examples exclude the helper dependency from Release builds.

At this checkpoint:

- 76 framework tests pass, including refresh/state/native-control retention, no idle rebuild loop,
  failed edit isolation/recovery, and queued refresh of a closed/disposed window.
- A real isolated `dotnet watch` + Xvfb native app edit was visible after **0.602 s** in one run.
  PID, typed text, local counter, TextBox and Button identities remained unchanged. This is one
  supported literal edit on Linux, not an arbitrary-edit latency guarantee or cross-platform proof.
- Release core and desktop assembly SHA-256 values are **byte-identical** before/after. The
  Workspace and OCR Release dependency graphs exclude `MySharp.HotReload`.
- The 1,000 static-label fixture remains **0 B** managed UI-thread allocation per rebuild
  (0.1736 ms before / 0.1764 ms after in one comparison); 1,000 retained states also remain **0 B**.
  Timing variation in a single comparison is not evidence of a speedup or a regression. Other
  dynamic fixtures still have their pre-existing allocations. This excludes compositor/GPU costs.

The `dotnet watch` process and .NET's metadata-editing/JIT environment have their own development
costs; the measurements above do not claim the entire development session is free of overhead.

Evidence: `artifacts/hot-reload-{build,release-build,tests,smoke,watch}.log`,
`hot-reload-smoke.json`, `hot-reload-live.png`, and `hot-reload-performance*.json`.

Reproduce the real edit proof without touching repository source or an existing desktop session:

```sh
python3 scripts/hot-reload-smoke.py
```

The script creates a temporary consumer and its own Xvfb display, applies a supported source edit,
checks same-process/state/control preservation, captures the result and stops its own processes.

Official API reference: <https://learn.microsoft.com/en-us/visualstudio/debugger/hot-reload-metadataupdatehandler>
