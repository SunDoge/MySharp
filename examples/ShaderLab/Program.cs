using System.Diagnostics;
using System.Text.Json;
using SkiaSharp;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using MySharp;
using MySharp.ShaderLab;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var reportDirectory = args.Length == 2 && args[0] == "--smoke" ? Path.GetFullPath(args[1]) : null;
        var backgroundStats = new ShaderStats();
        var layerStats = new ShaderStats();
        ShaderViewport? background = null;
        LayerHost? host = null;
        UiView? sample = null;
        Button? sampleButton = null;
        TextBox? sampleInput = null;
        var clicks = 0;
        var input = "Native input / 中文";
        var animated = false;
        var wave = 0d;
        var grayscale = 0d;
        var time = 0f;
        MySharpApp.Run(cx =>
        {
            Interlocked.Increment(ref layerStats.UiBuilds);
            background ??= new ShaderViewport(backgroundStats, false);
            if (host is null)
            {
                sample = new UiView(body =>
                {
                    Interlocked.Increment(ref layerStats.SubtreeBuilds);
                    Ui.Column(body).Gap(12).Padding(24).Background("#FFFFFF").Children(() =>
                    {
                        Ui.Text(body, "Native controls → shader").FontSize(23).Bold().TextColor("#183D67");
                        Ui.Text(body, "Rendered as a retained command picture, sampled by a shader.").FontSize(13).Wrap().TextColor("#65656F");
                        sampleInput = Ui.TextInput(body, input, value => input = value).Label("Shader sample input").Native<TextBox>();
                        sampleButton = Ui.Button(body, $"Native button · {clicks}").Primary().OnClick(() => clicks++).Native<Button>();
                        Ui.Row(body).Gap(8).Children(() =>
                        {
                            Ui.Column(body).Width(64).Height(32).Background("#E34B65");
                            Ui.Column(body).Width(64).Height(32).Background("#39A780");
                            Ui.Column(body).Width(64).Height(32).Background("#387BDD");
                        });
                        });
                });
                host = new LayerHost(sample, layerStats);
            }
            var settings = new ShaderSettings(animated, time, (float)wave, (float)grayscale);
            background.Configure(settings); host.Output.Configure(settings);
            Ui.Column(cx).Padding(24).Gap(16).Fill().Background("#F6F6F7").Children(() =>
            {
                Ui.Text(cx, "Shader lab").FontSize(26).Bold();
                Ui.Text(cx, "Experimental SkSL rendering · public Avalonia APIs · no framework API changes").FontSize(13).TextColor("#65656F");
                Ui.Row(cx).Gap(12).Children(() =>
                {
                    Ui.Button(cx, animated ? "Pause" : "Animate").OnClick(() => animated = !animated);
                    Ui.Button(cx, "Step time").OnClick(() => time += 0.7f);
                    Ui.Button(cx, "Refresh subtree").OnClick(() => host.Refresh());
                    Ui.Button(cx, "Reset").OnClick(() => { wave = grayscale = 0; animated = false; });
                });
                Ui.Grid(cx, "*,*").Set(Grid.ColumnSpacingProperty, 20d).Children(() =>
                {
                    Ui.Column(cx).Gap(10).Children(() =>
                    {
                        Ui.Text(cx, "1 · Shader background").FontSize(16).Bold();
                        Ui.Control(cx, () => background).Height(300).Fill();
                    });
                    Ui.Column(cx).Cell(0, 1).Gap(10).Children(() =>
                    {
                        Ui.Text(cx, "2 · Color / distortion on a native subtree").FontSize(16).Bold();
                        Ui.Control(cx, () => host).Height(300).Fill();
                    });
                });
                Ui.Text(cx, $"Wave displacement · {wave:F0} DIP").FontSize(13);
                Ui.Slider(cx, wave, value => wave = value, 0, 8).Label("Wave displacement");
                Ui.Text(cx, $"Grayscale · {grayscale:P0}").FontSize(13);
                Ui.Slider(cx, grayscale, value => grayscale = value, 0, 1).Label("Grayscale");
                Ui.Text(cx, "Visual distortion keeps the original layout and hit targets. Snapshot refresh currently covers size, focus, text and button/key input; arbitrary animations/popups are not tracked.")
                    .FontSize(12).Wrap().TextColor("#65656F");
            });
        }, new WindowOptions
        {
            Title = "MySharp Shader Lab", Width = 1100, Height = 800, MinWidth = 1000, MinHeight = 740,
            Theme = ThemeVariant.Light,
            Closed = _ => sample?.Dispose(),
            Opened = async window =>
            {
                if (reportDirectory is null) return;
                Directory.CreateDirectory(reportDirectory);
                var checks = new Dictionary<string, object>();
                try
                {
                    async Task Settle(int ms = 180) => await Task.Delay(ms);
                    void Require(bool value, string message) { if (!value) throw new Exception(message); }
                    async Task Frame(float t, float w, float gray)
                    {
                        var before = Interlocked.Read(ref layerStats.Frames);
                        time = t; wave = w; grayscale = gray;
                        ((UiView)window.Content!).Rebuild();
                        await Settle();
                        Require(Interlocked.Read(ref layerStats.Frames) > before, "Effect message must render a new frame");
                    }
                    SKBitmap Capture(string name)
                    {
                        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Smoke captures currently require X11 and ImageMagick");
                        var path = Path.Combine(reportDirectory, name + ".png");
                        var start = new ProcessStartInfo("import") { UseShellExecute = false };
                        start.ArgumentList.Add("-window");
                        start.ArgumentList.Add($"0x{window.TryGetPlatformHandle()!.Handle.ToInt64():x}");
                        start.ArgumentList.Add(path);
                        using var capture = Process.Start(start)!;
                        if (!capture.WaitForExit(5000) || capture.ExitCode != 0) throw new Exception("Native capture failed");
                        return SKBitmap.Decode(path);
                    }
                    await Settle(600);
                    host!.Refresh(); await Settle();
                    Require(layerStats.Error is null && backgroundStats.Error is null, layerStats.Error ?? backgroundStats.Error ?? "Shader initialization failed");
                    Require(layerStats.Frames > 0 && backgroundStats.Frames > 0, "Both shaders must actually draw");
                    using var original = Capture("original");
                    var inputControl = sampleInput!; var button = sampleButton!;
                    // The Python native smoke clicks and types into these actual coordinates.
                    await File.WriteAllTextAsync(Path.Combine(reportDirectory, "targets.json"), JsonSerializer.Serialize(new
                    {
                        button = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window),
                        input = inputControl.TranslatePoint(new Point(30, inputControl.Bounds.Height / 2), window)
                    }));
                    if (Environment.GetEnvironmentVariable("MYSHARP_SHADER_NATIVE_INPUT") == "1")
                    {
                        for (var i = 0; i < 100 && !File.Exists(Path.Combine(reportDirectory, "input-done")); i++) await Settle(50);
                        Require(clicks == 1 && input.Contains("typed-through-shader"), "Actual native hit/focus/typing must reach transparent-host controls");
                        checks["native_pointer_keyboard"] = true;
                    }
                    else checks["native_pointer_keyboard"] = "not requested";
                    button.Focus();
                    host!.Refresh(); await Settle();
                    using var afterInput = Capture("after-input");
                    Require(ReferenceEquals(button, sampleButton) && ReferenceEquals(inputControl, sampleInput), "Effects must retain native controls");
                    checks["retained_controls"] = true;
                    await Frame(0, 0, 1);
                    using var gray = Capture("grayscale");
                    var origin = host.TranslatePoint(default, window)!.Value;
                    var colorPixels = 0; var nonGrayPixels = 0; var changed = 0;
                    for (var y = (int)((origin.Y + 8) * window.RenderScaling); y < (origin.Y + 292) * window.RenderScaling; y += 3)
                    for (var x = (int)((origin.X + 8) * window.RenderScaling); x < (origin.X + host.Bounds.Width - 8) * window.RenderScaling; x += 3)
                    {
                        var a = afterInput.GetPixel(x, y); var b = gray.GetPixel(x, y);
                        if (Math.Max(a.Red, Math.Max(a.Green, a.Blue)) - Math.Min(a.Red, Math.Min(a.Green, a.Blue)) > 20) colorPixels++;
                        if (Math.Max(b.Red, Math.Max(b.Green, b.Blue)) - Math.Min(b.Red, Math.Min(b.Green, b.Blue)) > 3) nonGrayPixels++;
                        if (a != b) changed++;
                    }
                    Require(colorPixels > 100 && nonGrayPixels == 0 && changed > 100, "Actual subtree output must become grayscale");
                    checks["grayscale_pixels"] = new { colorPixels, nonGrayPixels, changed };
                    var buttonBounds = button.Bounds;
                    var buttonOrigin = button.TranslatePoint(default, window);
                    await Frame(1.2f, 8, 0);
                    using var distorted = Capture("distortion");
                    var differences = 0;
                    for (var y = (int)((origin.Y + 10) * window.RenderScaling); y < (origin.Y + 280) * window.RenderScaling; y += 2)
                    for (var x = (int)((origin.X + 10) * window.RenderScaling); x < (origin.X + host.Bounds.Width - 10) * window.RenderScaling; x += 2)
                        if (afterInput.GetPixel(x, y) != distorted.GetPixel(x, y)) differences++;
                    Require(differences > 200, "Distortion must change pixels");
                    Require(button.Bounds == buttonBounds && button.TranslatePoint(default, window) == buttonOrigin, "Distortion does not alter native layout");
                    checks["distortion_changed_pixels"] = differences;
                    if (Environment.GetEnvironmentVariable("MYSHARP_SHADER_NATIVE_INPUT") == "1")
                    {
                        File.WriteAllText(Path.Combine(reportDirectory, "distortion-ready"), "");
                        for (var i = 0; i < 100 && !File.Exists(Path.Combine(reportDirectory, "distortion-input-done")); i++) await Settle(50);
                        Require(clicks == 2, "Native button must remain clickable while visually distorted");
                        checks["native_click_during_distortion"] = true;
                        await Settle();
                    }
                    var uiBuilds = Interlocked.Read(ref layerStats.UiBuilds);
                    var snapshots = Interlocked.Read(ref layerStats.Snapshots);
                    var subtreeBuilds = Interlocked.Read(ref layerStats.SubtreeBuilds);
                    var layers = Interlocked.Read(ref layerStats.LayerRenders);
                    var frames = Interlocked.Read(ref layerStats.Frames);
                    host.Output.Configure(new(true, 1.2f, 8, 0));
                    background!.Configure(new(true, 0, 0, 0));
                    await Settle(900);
                    Require(layerStats.Frames > frames + 5, "Animation must advance on render thread");
                    Require(layerStats.UiBuilds == uiBuilds && layerStats.SubtreeBuilds == subtreeBuilds && layerStats.Snapshots == snapshots && layerStats.LayerRenders == layers,
                        "Uniform-only animation must not rebuild UI, capture the subtree or redraw its layer");
                    checks["animation_frames"] = layerStats.Frames - frames;
                    checks["animation_ui_builds"] = layerStats.UiBuilds - uiBuilds;
                    checks["animation_subtree_builds"] = layerStats.SubtreeBuilds - subtreeBuilds;
                    checks["animation_snapshots"] = layerStats.Snapshots - snapshots;
                    checks["animation_layer_renders"] = layerStats.LayerRenders - layers;
                    Require(layerStats.SurfaceCreates == 1, "Fixed-size input updates must reuse their offscreen surface");
                    host.Output.Configure(new(false, 1.2f, 8, 0)); background.Configure(new(false, 0, 0, 0));
                    for (var i = 0; i < 40 && (Volatile.Read(ref layerStats.AnimationRunning) || Volatile.Read(ref backgroundStats.AnimationRunning)); i++) await Settle(25);
                    Require(!layerStats.AnimationRunning && !backgroundStats.AnimationRunning, "Render thread must acknowledge pause");
                    await Settle(); frames = layerStats.Frames;
                    var animationTicks = layerStats.AnimationTicks;
                    await Settle(350);
                    Require(layerStats.AnimationTicks == animationTicks, "Paused shader must stop scheduling animation frames");
                    checks["paused_animation_ticks"] = layerStats.AnimationTicks - animationTicks;
                    checks["paused_extra_draws"] = layerStats.Frames - frames;
                    var oldSurfaces = layerStats.SurfaceCreates;
                    window.Width += 80; window.Height += 40;
                    await Settle(300);
                    Require(layerStats.SurfaceCreates > oldSurfaces && input.Contains("typed-through-shader") && clicks == 2,
                        "Resize must replace the offscreen surface while retaining edited native state");
                    checks["resize_retains_input"] = true;
                    host.Output.Release(); background.Release(); await Settle();
                    Require(layerStats.SurfaceDisposes == layerStats.SurfaceCreates, "Every created offscreen surface must be disposed");
                    Require(layerStats.Releases == 1 && backgroundStats.Releases == 1, "Render-thread resources must release exactly once");
                    checks["release"] = true;
                }
                catch (Exception error) { checks["error"] = error.ToString(); Environment.ExitCode = 1; }
                finally
                {
                    object Stats(ShaderStats s) => new { s.Frames, s.LayerRenders, s.UiBuilds, s.SubtreeBuilds, s.Snapshots, s.SurfaceCreates, s.SurfaceDisposes, s.LayerPixelBytes, s.GpuContext, s.GpuLayer, s.Releases, s.AnimationTicks, s.Error,
                        draw_callback_mean_ms = s.Frames > 0 ? s.DrawTicks * 1000d / Stopwatch.Frequency / s.Frames : 0,
                        draw_callback_mean_thread_bytes = s.Frames > 0 ? s.DrawBytes / s.Frames : 0,
                        snapshot_mean_ms = s.Snapshots > 0 ? s.SnapshotTicks * 1000d / Stopwatch.Frequency / s.Snapshots : 0,
                        snapshot_mean_thread_bytes = s.Snapshots > 0 ? s.SnapshotBytes / s.Snapshots : 0 };
                    await File.WriteAllTextAsync(Path.Combine(reportDirectory, "report.json"), JsonSerializer.Serialize(new
                    {
                        scale = window.RenderScaling, checks, background = Stats(backgroundStats), layer = Stats(layerStats),
                        limitation = "Prototype; command recording and draw callback timings include warmup, exclude GPU completion and input latency. GRContext presence does not establish hardware GPU acceleration."
                    }, new JsonSerializerOptions { WriteIndented = true }));
                    window.Close();
                }
            }
        }, args, configureApp: builder =>
        {
            if (Environment.GetEnvironmentVariable("MYSHARP_SHADER_SOFTWARE") == "1")
                builder.With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Software] });
            else if (Environment.GetEnvironmentVariable("MYSHARP_SHADER_GL_TEST") == "1")
                builder.With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Glx], GlxRendererBlacklist = [] });
        });
    }
}
