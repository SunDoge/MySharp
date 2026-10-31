# Native service verification

`UiContext.TopLevel`, `Clipboard`, `Storage`, `Screens`, `Launcher` and `PlatformHandle`
resolve the containing native TopLevel after visual attachment. Before attachment they may
be null. Access them on the UI thread, typically in an event callback; perform UI-affine
async work with `UiTask.RunOnUi` when reactive pending/error/cancellation state is useful.
Native async operations are not necessarily interruptible: check cancellation and the
current document/version before applying their result.

## Linux/X11 smoke

```bash
$HOME/.dotnet/dotnet build tests/MySharp.NativeSmoke -m:1 --disable-build-servers
bash scripts/native-services-smoke.sh
```

The executable uses the real MySharp desktop host, two native windows and a generated
fixture directory. It verifies:

- Unicode text clipboard transfer in both directions, then clear.
- StorageProvider file lookup/read, folder enumeration, create/write/delete.
- Nonempty screen enumeration and a nonzero native platform handle.
- A native GTK file picker selecting the fixture and returning the exact expected content.

The script uses a private Xvfb display, isolated XDG config/data/cache directories, a bounded
process timeout, strict shell failure handling and a required success flag in the JSON report.
It does not accept an old report as evidence; prior markers and reports are removed before
running. The fixture directory is deleted afterward. The report and picker screenshot are
local artifacts: `artifacts/native-services-smoke.json` and `artifacts/native-picker.png`.

This test explicitly sets `X11PlatformOptions.UseDBusFilePicker = false`. The default desktop
Portal path did not produce a dialog in the isolated test display, so the successful result
proves the GTK path, not Portal integration. The GTK backend emitted a nonfatal widget-event
diagnostic during automated selection; content validation and cleanup still completed.

Launcher presence is recorded, but opening a URI/application is not tested. Native tray,
Portal, other window managers and Windows/macOS behavior still require their own checks.
No real external endpoint is opened by this smoke.
