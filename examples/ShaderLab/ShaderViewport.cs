using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using SkiaSharp;

namespace MySharp.ShaderLab;

// Experimental renderer; no dependency is added to the framework packages.
internal sealed record ShaderSettings(bool Animated, float Time, float Wave, float Grayscale);
internal sealed record LayerPicture(SKPicture Picture, Size Size, double Scale);
internal sealed record ReleaseShader;

internal sealed class ShaderStats
{
    public long Frames, LayerRenders, Messages, Snapshots, UiBuilds, Releases, AnimationTicks, SubtreeBuilds, SurfaceCreates, SurfaceDisposes;
    public bool AnimationRunning;
    public long DrawTicks, DrawBytes, SnapshotTicks, SnapshotBytes, LayerPixelBytes;
    public bool SkiaAvailable, GpuContext, GpuLayer;
    public string? Error;
}

internal sealed class ShaderViewport(ShaderStats stats, bool layer) : Control
{
    private CompositionCustomVisual? visual;
    private ShaderSettings settings = new(false, 0, 0, 0);
    private bool released;
    public void Configure(ShaderSettings value)
    {
        settings = value;
        visual?.SendHandlerMessage(value);
    }
    public void SetPicture(LayerPicture picture)
    {
        if (visual is null || released) { picture.Picture.Dispose(); return; }
        visual.SendHandlerMessage(picture);
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        released = false;
        var compositor = ElementComposition.GetElementVisual(this)!.Compositor;
        visual = compositor.CreateCustomVisual(new ShaderHandler(stats, layer));
        ElementComposition.SetElementChildVisual(this, visual);
        UpdateSize(); visual.SendHandlerMessage(settings);
    }
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e); UpdateSize();
    }
    private void UpdateSize()
    {
        if (visual is not null) visual.Size = new Vector2((float)Bounds.Width, (float)Bounds.Height);
    }
    public void Release()
    {
        if (released) return;
        released = true;
        visual?.SendHandlerMessage(new ReleaseShader());
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Release(); ElementComposition.SetElementChildVisual(this, null);
        visual = null; base.OnDetachedFromVisualTree(e);
    }
}

internal sealed class ShaderHandler(ShaderStats stats, bool layer) : CompositionCustomVisualHandler
{
    private const string BackgroundSource = """
        uniform float time;
        half4 main(float2 p) {
            float v = 0.5 + 0.5 * sin(p.x / 42 + time + sin(p.y / 28 + time * 0.4));
            return half4(mix(half3(0.08, 0.16, 0.28), half3(0.18, 0.64, 0.76), v), 1);
        }
        """;
    private const string LayerSource = """
        uniform shader content;
        uniform float time;
        uniform float wave;
        uniform float grayscale;
        half4 main(float2 p) {
            float2 q = p + float2(0, wave * sin(p.x / 24 + time));
            half4 c = content.eval(q);
            half l = dot(c.rgb, half3(0.2126, 0.7152, 0.0722));
            return half4(mix(c.rgb, half3(l), grayscale), c.a);
        }
        """;
    private SKRuntimeEffect? effect;
    private SKRuntimeEffectUniforms? uniforms;
    private SKRuntimeEffectChildren? children;
    private SKPaint? paint;
    private SKPicture? picture;
    private SKSurface? surface;
    private SKImage? image;
    private SKShader? imageShader;
    private Size pictureSize;
    private double scale = 1;
    private int surfaceWidth, surfaceHeight;
    private IntPtr surfaceContext;
    private ShaderSettings settings = new(false, 0, 0, 0);
    private bool dirty, disposed;
    private double origin;

    public override void OnMessage(object message)
    {
        Interlocked.Increment(ref stats.Messages);
        if (disposed)
        {
            if (message is LayerPicture late) late.Picture.Dispose();
            return;
        }
        switch (message)
        {
            case ShaderSettings value:
                if (value.Animated && !settings.Animated) origin = CompositionNow.TotalSeconds - value.Time;
                settings = value;
                Volatile.Write(ref stats.AnimationRunning, value.Animated);
                Invalidate();
                if (value.Animated) RegisterForNextAnimationFrameUpdate();
                break;
            case LayerPicture value:
                picture?.Dispose(); picture = value.Picture;
                pictureSize = value.Size; scale = value.Scale; dirty = true;
                Invalidate(); break;
            case ReleaseShader:
                disposed = true;
                Volatile.Write(ref stats.AnimationRunning, false);
                imageShader?.Dispose(); image?.Dispose(); DisposeSurface(); picture?.Dispose();
                paint?.Dispose(); effect?.Dispose();
                Interlocked.Increment(ref stats.Releases);
                break;
        }
    }
    private void DisposeSurface()
    {
        if (surface is null) return;
        surface.Dispose(); surface = null;
        Interlocked.Increment(ref stats.SurfaceDisposes);
    }
    public override void OnAnimationFrameUpdate()
    {
        if (!disposed && settings.Animated)
        {
            Interlocked.Increment(ref stats.AnimationTicks);
            Invalidate(); RegisterForNextAnimationFrameUpdate();
        }
    }
    public override void OnRender(ImmediateDrawingContext context)
    {
        if (disposed) return;
        var start = Stopwatch.GetTimestamp();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        try
        {
            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null) { stats.Error = "No Skia API lease on this backend"; return; }
            using var lease = feature.Lease();
            stats.SkiaAvailable = true; stats.GpuContext = lease.GrContext is not null;
            if (effect is null)
            {
                effect = SKRuntimeEffect.CreateShader(layer ? LayerSource : BackgroundSource, out var error)
                    ?? throw new InvalidOperationException(error);
                uniforms = new SKRuntimeEffectUniforms(effect);
                children = new SKRuntimeEffectChildren(effect);
                paint = new SKPaint { IsAntialias = true };
            }
            if (layer && picture is null) return;
            if (layer && dirty)
            {
                imageShader?.Dispose(); image?.Dispose();
                var info = new SKImageInfo(Math.Max(1, (int)Math.Ceiling(pictureSize.Width * scale)),
                    Math.Max(1, (int)Math.Ceiling(pictureSize.Height * scale)), SKColorType.Rgba8888, SKAlphaType.Premul);
                // Compatible GPU surface when available; an explicit raster fallback otherwise.
                var gpuHandle = lease.GrContext?.Handle ?? IntPtr.Zero;
                if (surface is null || surfaceWidth != info.Width || surfaceHeight != info.Height || surfaceContext != gpuHandle)
                {
                    DisposeSurface();
                    surface = lease.GrContext is {} gpu ? SKSurface.Create(gpu, false, info) : SKSurface.Create(info);
                    if (surface is null) throw new InvalidOperationException("Offscreen surface creation failed");
                    surfaceWidth = info.Width; surfaceHeight = info.Height; surfaceContext = gpuHandle;
                    Interlocked.Increment(ref stats.SurfaceCreates);
                    stats.LayerPixelBytes = (long)info.Width * info.Height * 4;
                }
                stats.GpuLayer = surface.Context is not null;
                surface.Canvas.ResetMatrix();
                surface.Canvas.Clear(SKColors.Transparent);
                surface.Canvas.Scale((float)scale);
                surface.Canvas.DrawPicture(picture!);
                image = surface.Snapshot();
                imageShader = image.ToShader(SKShaderTileMode.Decal, SKShaderTileMode.Decal,
                    new SKSamplingOptions(SKFilterMode.Linear), SKMatrix.CreateScale((float)(1 / scale), (float)(1 / scale)));
                dirty = false; Interlocked.Increment(ref stats.LayerRenders);
            }
            uniforms!["time"] = settings.Animated ? (float)(CompositionNow.TotalSeconds - origin) : settings.Time;
            if (layer)
            {
                uniforms["wave"] = settings.Wave;
                uniforms["grayscale"] = settings.Grayscale;
                children!["content"] = imageShader;
            }
            using var shader = layer ? effect.ToShader(uniforms, children!) : effect.ToShader(uniforms);
            paint!.Shader = shader;
            var canvas = lease.SkCanvas;
            var saved = canvas.Save();
            try { canvas.DrawRect(0, 0, (float)EffectiveSize.X, (float)EffectiveSize.Y, paint); }
            finally { canvas.RestoreToCount(saved); paint.Shader = null; }
            Interlocked.Increment(ref stats.Frames);
        }
        catch (Exception error) { stats.Error = error.ToString(); }
        finally
        {
            Interlocked.Add(ref stats.DrawTicks, Stopwatch.GetTimestamp() - start);
            Interlocked.Add(ref stats.DrawBytes, GC.GetAllocatedBytesForCurrentThread() - allocated);
        }
    }
}
