# Input and popups

Native Avalonia still dispatches input and owns popup placement, focus and light dismissal.
MySharp retains declarations and handles their removal.

## Keyboard and routed events

```csharp
Ui.Button(cx, "Run").HotKey(runShortcut).OnClick(Start);
Ui.TextInput(cx, query).TabIndex(1).TabStop().AutomationId("query");
Ui.Control<Canvas>(cx).Focusable().On(InputElement.KeyDownEvent,
    (_, args) => HandleKey(args), RoutingStrategies.Tunnel, handledEventsToo: true);
```

Create reusable `KeyGesture` and `Cursor` values outside the build when possible.
`HotKey` supports native Button, MenuItem and SplitButton; unsupported controls throw.
`Focusable`, `TabStop`, `TabIndex`, `Cursor` and `AutomationId` set the inner native control.
Omitting a modifier clears its local property, allowing the native theme/default to apply.
A routed event has one declaration slot per event per element; a later declaration replaces
its handler and routing options. Changing options reattaches once, and omission detaches.
Default routing remains `Direct | Bubble`, without already-handled events.
For pointer capture, focus or custom input controls, use build-local `Native<T>()` access and
normal Avalonia event arguments; do not store an Element handle beyond the build.

## Owned MySharp flyouts

```csharp
var details = cx.Flyout(body =>
{
    var clicks = body.State(0);
    Ui.Column(body).Gap(8).Children(() =>
    {
        Ui.Text(body, "Details");
        Ui.Button(body, $"Count: {clicks.Value}").OnClick(() => clicks.Value++);
    });
});
Ui.Button(cx, "Open details").Flyout(details);
Ui.Text(cx, "Right-click here").ContextFlyout(details);
```

Use separate owned flyouts for targets that must have independent content/state. Flyout
content builds only when visually attached. Closing releases its local state, subscriptions,
resources and tasks; reopening starts a fresh content lifetime. Keep durable data in parent
or application-owned State handles. Parent rebuilds refresh an open flyout with the latest
builder. Removing the declaring scope or closing the owner disposes the flyout and content.

`UiFlyout.Native` exposes placement and other native Flyout options. `ShowAt(Control)` and
`Hide()` are available for event-driven opening. These operations run on the UI thread.
A native closing handler can cancel native dismissal; callers using that advanced behavior
remain responsible for its interaction policy.

## Native attachments

`element.ContextMenu(nativeMenu)`, `ContextFlyout(nativeFlyout)` and
`Flyout(nativeFlyout)` attach existing native objects. `Flyout` requires a Button.
Replacement, declaration omission and node unmount close the previous attachment. These
methods do not own arbitrary native content or its resources: use `cx.Flyout` for owned
MySharp content, or `cx.Resource` for separately owned application resources. Native menu
items, commands and hierarchical menu templates remain directly accessible.

## Verification

The headless suite verifies lazy content construction, retained identity, capture refresh,
hide/reopen cleanup, state unsubscription and owner-scope disposal; native menu/flyout
replacement, omission and unmount; routed-event options; actual keyboard dispatch for Tab
navigation and hotkeys, including disabled and removed registrations. `scripts/screenshot-popup.sh`
additionally opens the lab flyout under Linux Xvfb, closes it with Escape and starts its async
task with Ctrl+L. Screenshots verify that native path, not Windows/macOS popup behavior.

The screenshot script can exercise a published native binary too:

```bash
MYSHARP_NATIVE_BINARY="$PWD/artifacts/publish/current-input-aot/MySharp.Workspace" \
  bash scripts/screenshot-popup.sh
```

This AOT path was run and visually verified at the current checkpoint.
