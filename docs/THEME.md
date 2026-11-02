# Default desktop theme

MySharpApp installs the code-only `MySharpTheme` over native Fluent control templates.
The palette and metrics refer to the local MyGo `ui/theme.go`: zinc neutrals, blue accent,
6px control corners, restrained one-pixel borders and a four-pixel spacing basis. Buttons
have 34px minimum height; text/select controls use the same 34px minimum height and consistent padding.
Native focus, input, accessibility and popup behavior remain Avalonia's responsibility.

The theme includes Light/Dark resources and neutral/accent hover, pressed and disabled colors.
Primary buttons use #2563EB with white text in both variants to maintain readable contrast.
Custom application styles and local properties can override it as usual. No XAML or new
rendering backend is introduced; theme resources are constructed at application startup.

For a custom Avalonia host, install before creating windows:

```csharp
application.Styles.Add(MySharpTheme.CreateFluentBase());
application.Styles.Add(new MySharpTheme());
```

The Workspace pages use matching semantic colors in both modes:

```sh
dotnet run -- --page=lab
dotnet run -- --page=lab --dark
bash scripts/screenshot.sh
```

The screenshot script captures all seven light pages, custom chrome, a dark lab and the dark component gallery.
Headless tests cover actual native template colors, control metrics, hover input dispatch,
live Light/Dark switching and disabled text. The current framework checkpoint passes 102 tests. The build succeeds with existing nullable warnings in the Gallery/tests. Native X11 screenshots are verified; older published/AOT binaries predate
this visual change and should be rebuilt to receive it. The benchmark host now installs
the actual MySharp theme; runtime fixtures are described below.

## Quiet actions and selection

The second MyGo reference pass uses `ui/toolbar.go`'s visually quiet buttons and the list's
selection treatment. `Class("quiet")` supplies compact, transparent buttons with hover/pressed
feedback; `Class("selected", selected)` adds a soft blue tool-selection surface. `Flat()` also
uses the quiet appearance while preserving its existing transparent-value fallback.

```csharp
Ui.Button(cx, "Select").Class("quiet").Class("selected", active).OnClick(Select);
```

Native list rows now have a rounded soft selection and a two-pixel leading accent marker,
so thumbnail imagery remains the main content. Selected rows and tools have distinct dark
palette resources. Keyboard focus and selection continue to use native Avalonia behavior.
These are appearance changes; the class does not introduce a new toolbar container contract.
Headless pointer input checks quiet hover/pressed states and live theme switching. The OCR
workbench uses the shared appearance for its existing tools and thumbnail list.

## Shared component finish

The default palette now covers checkbox/radio indicators, switches, checked toggle
buttons, slider tracks/thumbs, dropdown rows and their popup surfaces, flyouts,
menus and inverse tooltips. Native light/dark switches update these resources live.
Small checked controls use MyGo's brighter #3B82F6 in dark mode; primary action
buttons retain #2563EB for white-label contrast. Placeholders use full opacity so
secondary input text remains readable. Sliders use a 4px track and 16px thumb;
checkboxes use a 4px corner on their native 20px indicator.

Switches have a code-only inline template with a 40×22 track and a 14px knob.
It retains the native named knob parts, checked/hover/disabled states, pointer and
keyboard toggling, and dragging. The redundant default On/Off captions are hidden;
explicit `ToggleSwitch.OnContent`/`OffContent` values remain available. The Settings
example uses these switches instead of hand-built On/Off buttons.

```sh
dotnet run -- --page=controls
dotnet run -- --page=controls --dark
```

The gallery uses default control appearances without individual color/size fixes.
Generate light/dark screenshots locally with `bash scripts/screenshot.sh` (`artifacts/controls*.png`).
Headless checks cover native switch hover, click, Space activation, knob dragging,
checked/disabled palettes, and dropdown popup colors after a live theme change.
This pass adds no rebuild loop or per-frame theme logic. Performance and AOT sizes
have not been remeasured for this theme revision.

## Navigation targets and task rows

`Button.navigation` stretches the native button inside its layout wrapper in both
axes. Combine it with `quiet` and `selected` for full-row navigation; the example
uses a 42px row. The pressed state keeps the hit target fixed instead of scaling
it. Selection is painted by the button itself, so the highlighted area and click
area coincide.

Task rows use a native checkbox containing the title, rather than a separate
Unicode circle button. The native template aligns its indicator and label; the
project and due columns are vertically centered. Both the indicator and task
label toggle completion. Pointer tests exercise navigation near its top/right
and bottom/right edges and task text/indicator clicks in both theme variants.

## Vector icons

The core package uses Lucide.Avalonia 0.2.26 (MIT, Lucide/Feather artwork notices
in `licenses/`). Its dependency baseline is Avalonia 11; the pinned binary is
validated here against Avalonia 12.1.4 by headless tests and native X11 screenshots.
NativeAOT has not been revalidated for this dependency revision.

```csharp
Ui.Icon(cx, Icons.Settings);
Ui.Button(cx, "Settings").Icon(Icons.Settings).OnClick(OpenSettings);
Ui.IconButton(cx, Icons.Trash, "Delete task").OnClick(DeleteTask);
```

`Icons` provides common constants; the full set is available as
`Lucide.Avalonia.LucideIconKind`. Icons default to 16px with an 8px label gap.
Standalone icons accept `.TextColor(...)`; button icons inherit native label
colors, including selection, primary, hover and disabled states. Icon-only
buttons require an accessible label, also used as a tooltip, and have a 34px
minimum hit target in the desktop theme.

Buttons keep their string Content. A shared recycling data template updates
kind, size and label without creating a new icon on unchanged builds or text
changes. Omitting `.Icon(...)` restores the native text-only presentation.
There are no SVG parsers, reflection bindings or per-frame reconstruction in
this adapter; the upstream package lazily parses and caches vector geometry.
Its packaged icon data is included with the core dependency.

## Typography and segmented groups

`MySharpFontFamily` is a dynamic theme resource shared by windows and popup roots.
It resolves Latin text through the registered Inter collection, then tries
PingFang SC, Microsoft YaHei UI, Noto Sans CJK SC and Noto Sans CJK JP for CJK.
This avoids an unspecified system fallback selecting an editor's monospace font.
The native probe on this Linux machine found Sarasa Mono CL before the change
and Noto Sans CJK SC after it. These are observations of this machine, not a claim
about every Linux installation. CJK fonts are supplied by the system, not bundled.
Custom hosts should register the font collection with `.WithInterFont()` as the
MySharp desktop host does. Override `application.Resources["MySharpFontFamily"]`
with a `FontFamily` to use another UI family.

Text rendering/hinting defaults remain platform-controlled. The diagnostic probe
compares actual shaped font runs rather than assuming a font from its requested
name. `bash scripts/typography-smoke.sh` renders 12/14/16px mixed-script labels,
inputs, buttons and a segmented group at real X11 scale factors 1 and 1.5. The
report records the chosen font faces and missing glyphs, and screenshots use
physical pixel sizes and matching DPI. Outputs stay in local `artifacts/`.

```csharp
Ui.Text(cx, "Section title").Weight(Avalonia.Media.FontWeight.Medium);
Ui.Segmented(cx, new[] { "Original", "Translated", "Both" }, selectedIndex);
```

Use stable item collections in repeating declarations. Segmented groups use native
ListBox selection, recycling and horizontal containers, with a neutral raised
selected face instead of the regular list's leading stripe. Pointer padding,
arrow-key selection, live light/dark colors, disabled text and unchanged native
container identities are covered by tests. The item-panel factory is initialized
only by applications using segmented groups. Regular lists retain their normal
selection appearance. Default button/input/select metrics now share a 34px
minimum height; larger fonts and multi-line input can still grow naturally.

## Current verification

The gallery sidebar uses a scrolling navigation area and a bottom profile row,
without a fixed spacer. Pointer tests cover the navigation row edges, task label
alignment/clicks and the 960×680 minimum window in both variants.

`dotnet run -c Release --project benchmarks/MySharp.Benchmarks` measures declarations,
reconciliation and layout with the real theme installed. The static 1,000-label,
1,000-local-state and unchanged segmented fixtures can rebuild without managed
allocation after warmup. Ordinary example pages still allocate closures and other
objects. These fixtures exclude the native compositor/GPU and input latency;
they do not establish end-to-end or cross-framework rendering performance.

Remaining platform checks and API breadth are tracked separately in
[API-COVERAGE.md](API-COVERAGE.md). Linux/X11 evidence does not establish
Windows/macOS window-manager behavior or native macOS visual parity.

## Disclosures

`Ui.Expander` keeps native disclosure input and exposes controlled expansion state.
Its default header stretches across the available row, has a 36px minimum height,
and uses a borderless header. Content stays mounted when collapsed, so a draft
input survives closing and reopening. Pointer-edge and Space-key tests exercise
both variants, including retained input identity and exactly-once state changes.
The component gallery includes an editable disclosure example.

The gallery also contains a chapter tree, tabbed preview/history content and File/View menus. Native keyboard tests in both variants verify tree expansion/selection, tab content switching and menu action dispatch. Menu item event handlers belong to a scoped resource and detach when the sample unmounts.

Tab headers use 14px text and a 34px minimum height. Disclosure headers use the
same neutral ramp in light/dark, including hover, press and disabled states;
they no longer inherit an unrelated dark header fill. Native templates and focus
behavior remain intact. Resource names follow Avalonia's
[Expander template](https://github.com/AvaloniaUI/Avalonia/blob/12.0.0/src/Avalonia.Themes.Fluent/Controls/Expander.xaml)
and [TabItem template](https://github.com/AvaloniaUI/Avalonia/blob/12.0.0/src/Avalonia.Themes.Fluent/Controls/TabItem.xaml);
the installed runtime behavior is verified by the tests above.
