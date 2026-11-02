# Shader lab

Independent, code-only prototypes for MySharp on Avalonia 12.1.4 / SkiaSharp 3.119.4.
These do not add a shader dependency or public effect API to MySharp packages.

```sh
dotnet run -c Release --project examples/ShaderLab
```

Start paused. Use **Animate**, **Step time**, **Wave displacement**, and **Grayscale**.
The native input and button in the right-hand panel remain interactive. **Refresh
subtree** explicitly updates the recorded content when an untracked property changes.

## Implemented paths

1. A procedural SkSL background, drawn by `CompositionCustomVisualHandler` on the
   render thread. Animation is opt-in and requests frames only while active.
2. A native MySharp subtree rendered through a color/distortion shader. The UI thread
   records drawing commands into an `SKPicture` using Avalonia's public
   [DrawingContextHelper.RenderAsync](https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/src/Skia/Avalonia.Skia/Helpers/DrawingContextHelper.cs).
   The render thread replays them into a compatible GPU-context `SKSurface` when
   available, or an explicit raster surface otherwise. The resulting image becomes
   the shader's `content` input. There is no application-side `ReadPixels`,
   `RenderTargetBitmap`, or CPU bitmap roundtrip in the effect pipeline.

The offscreen surface is reused across same-size updates. Uniform-only animation
reuses the content image: it does not rebuild the UI, record a new picture, or
redraw the content layer. It still runs a shader/draw each frame and allocates
SkiaSharp wrappers; this is not a zero-allocation rendering claim.

The real subtree stays mounted inside an opacity-zero parent. Capture starts at
that parent's child, so its drawing remains available. The shader visual ignores
hit testing. Actual controls keep their original layout, pointer and keyboard
input; distortion only changes displayed pixels. Private visuals are encapsulated
inside the host control rather than declared as MySharp-managed child nodes.

## Reproduce the checks

Linux/X11 tools: `Xvfb`, `xdotool`, ImageMagick `import`, `glxinfo`, Python 3, .NET 10.

```sh
python3 scripts/shader-smoke.py
```

The runner builds Release and checks:

| Mode | Scale | GPU-context layer |
| --- | --- | --- |
| Forced GLX | 1 | Yes |
| Forced GLX | 1.5 | Yes |
| Explicit raster | 1 | No |

The current test environment uses **Mesa llvmpipe**, which implements OpenGL in
software. The GLX smoke explicitly clears Avalonia's renderer blacklist only for
this test process to exercise the GPU-context API path. It does not establish
hardware GPU performance or change normal application defaults.

The native smoke types into the input, clicks the button before and during
distortion, and checks actual window pixels for grayscale and wave displacement.
It verifies control identity, unchanged layout, edited state after resize, no UI
or subtree builds/picture captures/content-layer renders during uniform animation,
no animation ticks after pause, and balanced surface creation/disposal.

Screenshots and JSON reports go to ignored `artifacts/shaders/`. Captures use the
real X11 window: a `RenderTargetBitmap` capture does not exercise this custom
composition visual in the same way. Capture/readback is test instrumentation,
not part of the effect implementation.

Report timings measure callback CPU time, include warmup/compilation, and exclude
GPU completion, full-frame latency and input latency. They must not be interpreted
as GPU frame times or used to rank rendering frameworks.

## Deliberate limitations

- This is a prototype, not a general `.Effect(...)` API. It pins low-level Avalonia
  APIs that can change across versions.
- Snapshot refresh currently covers size/DPI, focus, text, button and key input,
  plus explicit refresh. Arbitrary property changes, pointer selection, caret blink,
  hover animations and independently animated descendants are not fully tracked.
- Popups and native hosted windows are outside this recorded subtree. Accessibility
  behavior with the hidden original subtree has not been verified.
- Distortion samples are transparent outside the captured rectangle and are clipped
  to the host. There is no MyGo-style `MaxSampleOffset` padding yet.
- No general backdrop capture, multi-pass graph, context-loss recovery, allocation
  budget or production fallback policy is implemented.
- Offscreen rendering can change text antialiasing compared with direct native
  drawing. Windows/macOS, real hardware GPUs and NativeAOT have not been verified.

`LayerHost.cs` owns command recording and the native subtree. `ShaderViewport.cs`
owns render-thread shaders, textures, surfaces, pause and release messages.
`Program.cs` provides the UI and native smoke assertions.
