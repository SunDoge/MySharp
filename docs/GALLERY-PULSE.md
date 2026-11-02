# Pulse Gallery demo

Run `dotnet run -- --page=pulse`, or select **Pulse** in the Gallery sidebar.
**Back to Gallery** returns to the other examples. The example follows the light
native-app screenshots at [pulse.egoist.dev](https://pulse.egoist.dev/), rather
than reproducing its marketing website. No original artwork is bundled.

This is an interactive demonstration with **synthetic dynamic data**, not a
system monitor. The workstation, processes, connections, and cleanup candidates
are all samples. Quit, Force Quit, and cleanup only modify this in-memory
sample dataset. No OS processes are signalled and no files are inspected or deleted.

- Overview: six metric cards, colored dot-matrix histories, memory allocation,
  application memory treemap, and application energy bars.
- Processes / Apps: case-insensitive name/PID search, category filters, sortable
  columns, full-width selectable rows, and a process inspector.
- CPU: stacked user/system history and ten simulated core meters.
- Memory, GPU, Disk, Network, Energy: individual history pages.
- Connections: process/host search. Cleanup: sample selection and simulated cleanup.
- The range buttons sample a bounded one-hour ring buffer. Pause stops the
  one-second timer; detaching the page also stops it. Returning preserves state.

The host uses `Ui.Control` to retain a native code-only Avalonia view. Native
controls preserve the search editor and caret during updates. Charts draw vector
dots directly through `DrawingContext`; there is no animation frame loop or
background polling when the page is hidden. The fixed sample process list is small
(24 rows); this example does not demonstrate large-list virtualization.

`PulseModel` is independent of Avalonia and handles samples, selection, filtering,
sorting, and mock actions. Hardware labels are sample metadata; the example does
not detect GPU, battery, temperatures, or real hardware.

Verification: the headless suite covers history bounds, filtering/sorting/selection,
pause, mock actions, retained editor/caret, timer lifecycle, and Gallery return.
`python3 scripts/pulse-smoke.py` captures native X11 Overview, processes, inspector,
CPU, and the minimum 960×680 window in ignored `artifacts/pulse/`.
