using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;

namespace MySharp.Examples.Game2048;

internal static class GameLook
{
    internal const string Paper = "#FAF8EF", Ink = "#776E65", Board = "#BBADA0", Light = "#F9F6F2", Button = "#8F7A66";
    internal const double BoardSize = 500, Gap = 15, TileSize = 106.25, Step = 121.25;
    internal static readonly IBrush PaperBrush = Brush.Parse(Paper);
    internal static readonly IBrush InkBrush = Brush.Parse(Ink);
    internal static readonly IBrush LightBrush = Brush.Parse(Light);
    internal static readonly TransformOperations[] Positions = Enumerable.Range(0, 16).Select(i =>
    {
        var builder = new TransformOperations.Builder(1);
        builder.AppendTranslate((i % 4) * Step, (i / 4) * Step);
        return builder.Build();
    }).ToArray();
    internal static IBrush TileBrush(long value) => Brush.Parse(value switch
    {
        2 => "#EEE4DA", 4 => "#EDE0C8", 8 => "#F2B179", 16 => "#F59563",
        32 => "#F67C5F", 64 => "#F65E3B", 128 => "#EDCF72", 256 => "#EDCC61",
        512 => "#EDC850", 1024 => "#EDC53F", 2048 => "#EDC22E", _ => "#3C3A32"
    });
    internal static async Task Play(Animation animation, Animatable target, CancellationToken token)
    {
        try { await animation.RunAsync(target, token); }
        catch (OperationCanceledException) { }
        catch (Exception error) { System.Diagnostics.Trace.TraceError("2048 animation failed: {0}", error); }
    }
}

// MyGo eases each keyframe interval separately; Avalonia's Animation.Easing maps
// the entire timeline. Preserve the two ease-in/out intervals around the pop peak.
internal sealed class MergePopEase : Easing
{
    private static readonly CubicEaseInOut Segment = new();
    public override double Ease(double progress) => progress < .5
        ? .5 * Segment.Ease(progress * 2)
        : .5 + .5 * Segment.Ease((progress - .5) * 2);
}

// Native visual transforms animate independently of MySharp declarations.
public sealed class NumberTileControl : UserControl, IDisposable
{
    private readonly Border face;
    private readonly bool merged;
    private readonly bool reducedMotion;
    private CancellationTokenSource? animation;
    public long Value { get; }
    public NumberTileControl(NumberTile tile, bool reducedMotion = false)
    {
        Value = tile.Value; merged = tile.Merged; this.reducedMotion = reducedMotion;
        Width = Height = GameLook.TileSize; IsHitTestVisible = false; ClipToBounds = false;
        HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Stretch;
        RenderTransform = GameLook.Positions[tile.Row * 4 + tile.Column];
        if (!reducedMotion) Transitions = new Transitions
        {
            new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(100), Easing = new CubicEaseInOut() }
        };
        var digits = tile.Value.ToString().Length;
        face = new Border
        {
            Background = GameLook.TileBrush(tile.Value), CornerRadius = new CornerRadius(3),
            RenderTransformOrigin = RelativePoint.Center, RenderTransform = new ScaleTransform(1, 1),
            Child = new TextBlock
            {
                Text = tile.Value.ToString(), FontSize = digits <= 2 ? 55 : digits == 3 ? 45 : digits == 4 ? 35 : digits == 5 ? 30 : 24,
                FontWeight = FontWeight.Bold, Foreground = tile.Value <= 4 ? GameLook.InkBrush : GameLook.LightBrush,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }
        };
        if (tile.Value is >= 128 and <= 2048)
        {
            var power = (int)Math.Log2(tile.Value) - 7;
            var alpha = .238 + power * .0795;
            face.BoxShadow = new BoxShadows(new BoxShadow
            {
                Blur = 30, Spread = 10, Color = Color.FromArgb((byte)(alpha * 255), 243, 215, 116)
            }, [new BoxShadow { IsInset = true, Spread = 1, Color = Color.FromArgb((byte)((.143 + power * .0475) * 255), 255, 255, 255) }]);
        }
        Content = face;
        AutomationProperties.SetName(this, $"{tile.Value}");
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (reducedMotion) return;
        animation = new CancellationTokenSource();
        StartAnimation(animation.Token);
    }
    private void StartAnimation(CancellationToken token)
    {
        var scale = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(200), Delay = TimeSpan.FromMilliseconds(100), FillMode = FillMode.Backward,
            Easing = merged ? new MergePopEase() : new CubicEaseInOut(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(ScaleTransform.ScaleXProperty, 0d), new Setter(ScaleTransform.ScaleYProperty, 0d) } },
                new KeyFrame { Cue = new Cue(.5), Setters = { new Setter(ScaleTransform.ScaleXProperty, merged ? 1.2 : .5), new Setter(ScaleTransform.ScaleYProperty, merged ? 1.2 : .5) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(ScaleTransform.ScaleXProperty, 1d), new Setter(ScaleTransform.ScaleYProperty, 1d) } }
            }
        };
        // A new tile is a single eased 0→1 ramp, without the merge's midpoint.
        if (!merged) scale.Children.RemoveAt(1);
        _ = GameLook.Play(scale, face, token);
        if (!merged)
        {
            var fade = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(200), Delay = TimeSpan.FromMilliseconds(100),
                FillMode = FillMode.Backward, Easing = new CubicEaseInOut(),
                Children =
                {
                    new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 0d) } },
                    new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 1d) } }
                }
            };
            _ = GameLook.Play(fade, face, token);
        }
    }
    public void Dispose()
    {
        if (animation is null) return;
        animation.Cancel(); animation.Dispose(); animation = null;
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    { Dispose(); base.OnDetachedFromVisualTree(e); }
}

public sealed class GameBoardCanvas : Canvas
{
    private readonly Game2048Session session;
    private IPointer? captured;
    private Point start;
    private Vector scroll;
    private long lastScroll;
    private bool scrollSpent;
    public GameBoardCanvas(Game2048Session session)
    {
        this.session = session; Focusable = true; Background = Brushes.Transparent;
        AutomationProperties.SetName(this, "2048 board");
        AutomationProperties.SetHelpText(this, "Arrow keys, WASD or HJKL move tiles. Drag or scroll to swipe. R starts a new game.");
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (session.Game.Stopped || captured is not null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus(); start = e.GetPosition(this); captured = e.Pointer; captured.Capture(this); e.Handled = true;
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (captured is null || !ReferenceEquals(captured, e.Pointer)) return;
        var delta = e.GetPosition(this) - start;
        CancelGesture();
        if (Direction(delta, 10) is {} direction) session.Move(direction);
        e.Handled = true;
    }
    internal static MoveDirection? Direction(Vector delta, double threshold)
    {
        if (Math.Max(Math.Abs(delta.X), Math.Abs(delta.Y)) < threshold) return null;
        return Math.Abs(delta.X) > Math.Abs(delta.Y)
            ? delta.X > 0 ? MoveDirection.Right : MoveDirection.Left
            : delta.Y > 0 ? MoveDirection.Down : MoveDirection.Up;
    }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (session.Game.Stopped || e.KeyModifiers != KeyModifiers.None) return;
        if (Environment.TickCount64 - lastScroll > 180) { scroll = default; scrollSpent = false; }
        lastScroll = Environment.TickCount64;
        if (!scrollSpent)
        {
            scroll -= e.Delta;
            if (Direction(scroll, 1) is {} direction) { scrollSpent = true; session.Move(direction); }
        }
        e.Handled = true;
    }
    public void CancelGesture()
    {
        var pointer = captured; captured = null; pointer?.Capture(null);
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    { CancelGesture(); base.OnPointerCaptureLost(e); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    { CancelGesture(); base.OnDetachedFromVisualTree(e); }
}
