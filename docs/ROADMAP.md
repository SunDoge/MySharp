# MySharp capability and verification plan

Objective: expose the capabilities Avalonia already provides, reduce frame/rebuild cost using profiling, and add state and async task APIs. This plan tracks the full objective; an implemented escape hatch alone does not prove all convenient APIs or platform behavior are complete.

## Workstreams and completion evidence

| Area | Required behavior | Verification |
| --- | --- | --- |
| Profiling | Current caller-location baseline; sampled stack/allocation evidence; phase attribution; before/after allocations and latency | Reproducible Release harness, stored profile/report, changed-state and unchanged-state measurements |
| Retained updates | Do not reset and reapply equal properties; preserve removed modifier reset, caret, focus, selection, list identity | Property notification tests, retention regression tests, phase measurements |
| State | External shared state and scoped local state; automatic read subscription; equality suppression; deterministic unmount/dispose; UI thread contract | Headless tests of updates, conditional reads, dynamic scopes, disposal, thread access |
| Async | Pending/success/error/canceled states; cancel/restart; stale result suppression; task errors observed; cancellation on unmount/dispose; UI dispatch | Controlled TaskCompletionSource tests including ignored cancellation, replacement and out-of-order completion |
| Native interoperability | Host arbitrary existing Avalonia controls once; configure strongly typed properties and events without repeated subscriptions; native templates/styles and attached properties remain accessible | Retained native control, event lifetime and AOT checks |
| Common controls | Forms (toggle, checkbox, radio, select, slider, numeric/date/time), display/progress, collections and templates, containers/navigation, menus/popups | Public API coverage matrix plus interactive gallery and behavioral tests |
| Layout and styling | Grid rows/cells, dock/wrap, alignment, sizing, scrolling, styles/classes, themes, accessibility, tooltips/context menus, input/focus | Layout/keyboard tests and rendered screenshots |
| Windows | System/custom/borderless chrome, title drag and native buttons, resize/state/activation/closing, owned/dialog windows; typed host escape hatch | Headless tests and native X11 smoke; record Windows/macOS limits honestly |
| Desktop services | Clipboard, storage pickers, screens, launcher, tray/menu, platform handles and lifetime hooks using Avalonia | API access tests, native smoke and documented platform coverage |
| Delivery | Examples for state/tasks/window chrome and controls, docs, clean Release/AOT publish, performance and functional regressions | Test suite, screenshot review, publish smoke and measured artifacts |

## Current state at goal start

Basic Column/Row/Grid/Scroll, Text/Button/TextInput, caller-location identity and explicit Scope exist. Source generator is no longer a dependency. Every rebuild currently clears and reapplies properties, parses brush strings, allocates build handles/frames/path strings and builds reconciliation sets. WindowOptions only exposes basic size/title/theme. No state or task API exists yet.

## Execution order

1. Record the current baseline and trace; implement measured retained-property and reconciliation optimizations while keeping behavior tests.
2. Add state and async task lifecycle primitives, test cancellation/races and expose a real example.
3. Fill native-control interoperability, convenient controls/layout/styles, window chrome and desktop-service surfaces. Maintain a capability matrix rather than declaring blanket parity from a small demo.
4. Verify the full matrix, examples, platform limits and publishing; only then audit objective completion.


## Current progress (active, not a completion claim)

- Property/reconciliation optimization and trace attribution implemented; sampled string costs motivated numeric IDs.
- State, owned resources, manual/keyed async work and UI-affine async starts implemented with race/lifetime tests.
- Value handles/owner retirement, numeric key domains, callback state passing and sparse buffer trimming adapted from MyGo's design. See MYGO-OPTIMIZATIONS.md.
- Native Control/Direct, properties/events, basic common controls, layout, window chrome/commands/modal hooks and TopLevel services implemented.
- Four-page lab and custom chrome screenshot verified. Current tests and pending areas are tracked by API-COVERAGE.md; full convenient control gallery, native row-template ownership, popup/menu/keyboard, real service and cross-platform/window-manager checks remain.
- Current combined AOT publish passed; its smoke verified overview/tasks/settings/lab/custom-chrome. Final combined untraced measurements are in profile-current-combined.json. A native/GPU trace and the remaining interaction matrix are not complete; earlier size/timing artifacts describe historical snapshots.

### Owned native data templates

Added `cx.Template<T>`: a retained `IDataTemplate` owns only visually attached row views.
Rows create local resources on attachment, release subscriptions/resources/tasks on detach,
and refresh with the latest parent builder. Reattachment deliberately starts a new local
lifetime; durable row state belongs in the item model. The 60-test suite covers lazy creation,
retained template identity, updated captures, detach unsubscribe and owner cleanup. Native
hierarchical/recycling template helpers and richer collection interaction coverage remain open.

### Cleanup failure handling

Owned-resource/node sweeps, view shutdown and owned template shutdown attempt all remaining
cleanup before reporting aggregate errors. Initial-build failure preserves its original error.
Disposed entries retire even if their disposer throws; control replacement can retry on a later
build. The 64-test headless suite covers shutdown, unmount, initial construction and replacement.

### Input, overlays and resource factories

Added retained input modifiers, HotKey and routed-event routing/handled options; owned
`cx.Flyout` and native flyout/context-menu attachments close and release on their declared
lifecycle boundaries. The 70-test suite includes actual Headless keyboard dispatch and popup
lifetime behavior. Linux Xvfb screenshots verify opening/dismissing a flyout and Ctrl+L task
startup in the lab (`scripts/screenshot-popup.sh`), without claiming cross-platform coverage.
Static resource constructors remove framework closure allocation on cache hits; a 1,000-state
steady fixture measures 0 B. Full capability work remains active: see API-COVERAGE.md.

### Live native services and themes

A separate real-X11 desktop smoke verifies cross-window Unicode clipboard transfer/clear,
storage file/folder APIs including create/write/delete, screen/handle access and real GTK
file-picker selection/content under isolated Xvfb/XDG directories. Portal and actual launcher
opening remain unverified. See NATIVE-SERVICES.md for reproduction and runtime diagnostics.
The Headless suite now passes 71 tests, adding live theme resource/class selector interaction
and conditional local override removal on the same retained native control. Animation,
reduced motion, richer galleries and platform-specific interaction work remain active.
