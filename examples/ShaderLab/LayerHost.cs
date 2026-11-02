using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Skia.Helpers;
using Avalonia.Threading;
using SkiaSharp;

namespace MySharp.ShaderLab;

// The invisible parent suppresses original drawing, but the actual native controls
// remain the input/focus targets. Accessibility has not been verified. Capture starts at its child, not at
// that transparent parent. This is a prototype, not a general dirty-subtree tracker.
internal sealed class LayerHost : UserControl
{
    private readonly Control content;
    private readonly ShaderStats stats;
    public ShaderViewport Output { get; }
    private bool queued, attached;
    private Size recordedSize;
    private double recordedScale;
    public LayerHost(Control content, ShaderStats stats)
    {
        this.content = content; this.stats = stats;
        Output = new ShaderViewport(stats, true) { IsHitTestVisible = false };
        var layout = new Grid();
        layout.Children.Add(new Border { Child = content, Opacity = 0 });
        layout.Children.Add(Output);
        Content = new Border { Child = layout, ClipToBounds = true };
        HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        content.SizeChanged += (_, _) => Refresh();
        LayoutUpdated += (_, _) =>
        {
            var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            if (content.Bounds.Size != recordedSize || scale != recordedScale) Refresh();
        };
        AddHandler(Button.ClickEvent, (_, _) => Refresh(), RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(TextBox.TextChangedEvent, (_, _) => Refresh(), RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(InputElement.GotFocusEvent, (_, _) => Refresh(), RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(InputElement.KeyUpEvent, (_, _) => Refresh(), RoutingStrategies.Bubble, handledEventsToo: true);
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    { base.OnAttachedToVisualTree(e); attached = true; Refresh(); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    { attached = false; base.OnDetachedFromVisualTree(e); }
    public void Refresh()
    {
        if (queued) return;
        queued = true;
        Dispatcher.UIThread.Post(() =>
        {
            queued = false;
            if (!attached || content.Bounds.Width < 1 || content.Bounds.Height < 1) return;
            var start = Stopwatch.GetTimestamp();
            var bytes = GC.GetAllocatedBytesForCurrentThread();
            recordedSize = content.Bounds.Size;
            recordedScale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            using var recorder = new SKPictureRecorder();
            var canvas = recorder.BeginRecording(new SKRect(0, 0, (float)recordedSize.Width, (float)recordedSize.Height));
            // Public helper records drawing commands. No RenderTargetBitmap/ReadPixels.
            DrawingContextHelper.RenderAsync(canvas, content, new Rect(recordedSize), new Vector(96, 96)).GetAwaiter().GetResult();
            Output.SetPicture(new LayerPicture(recorder.EndRecording(), recordedSize, recordedScale));
            Interlocked.Increment(ref stats.Snapshots);
            Interlocked.Add(ref stats.SnapshotTicks, Stopwatch.GetTimestamp() - start);
            Interlocked.Add(ref stats.SnapshotBytes, GC.GetAllocatedBytesForCurrentThread() - bytes);
        }, DispatcherPriority.Render);
    }
}
