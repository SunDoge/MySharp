using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Documents;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace MySharp.Examples.Game2048;

public sealed class Game2048View : UserControl, IDisposable
{
    private readonly Game2048Session session;
    private readonly UiView view;
    private GameBoardCanvas? board;
    private bool disposed;
    public UiView View => view;
    public Game2048Session Session => session;
    public Game2048View(Game2048Session session)
    {
        this.session = session;
        foreach (var state in new[] { "", "PointerOver", "Pressed", "Disabled" })
        {
            Resources["ButtonBackground" + state] = Brush.Parse(state == "PointerOver" ? "#988470" : state == "Pressed" ? "#846F5E" : GameLook.Button);
            Resources["ButtonForeground" + state] = GameLook.LightBrush;
            Resources["ButtonBorderBrush" + state] = Brushes.Transparent;
        }
        Styles.Add(new Style(x => x.OfType<Button>().Class("game2048"))
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, Brush.Parse(GameLook.Button)),
                new Setter(TemplatedControl.ForegroundProperty, GameLook.LightBrush),
                new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(0)),
                new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(3)),
                new Setter(TemplatedControl.FontSizeProperty, 18d),
                new Setter(TemplatedControl.FontWeightProperty, FontWeight.Bold),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(20, 0)),
                new Setter(Layoutable.HeightProperty, 40d)
            }
        });
        Styles.Add(new Style(x => x.OfType<Button>().Class("game2048").Class(":pressed"))
        {
            Setters = { new Setter(RenderTransformProperty, null) }
        });
        view = new UiView(Build);
        var viewport = new Viewbox
        {
            Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both, Child = view,
            MaxWidth = 800, MaxHeight = 1264,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        Content = new Border { Background = GameLook.PaperBrush, Padding = new Thickness(24), Child = viewport };
        HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Stretch;
        session.Changed += Changed;
        AddHandler(KeyDownEvent, Key, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Dispatcher.UIThread.Post(() => { if (!disposed && VisualRoot is not null) board?.Focus(); }, DispatcherPriority.Input);
    }
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        board?.CancelGesture(); base.OnSizeChanged(e);
    }
    private void Changed(bool restart)
    {
        if (disposed) return;
        view.Rebuild();
        if (!session.Game.Stopped) board?.Focus();
    }
    private void Key(object? sender, KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Escape) { board?.CancelGesture(); e.Handled = true; return; }
        if (e.Key == Avalonia.Input.Key.N && e.KeyModifiers is KeyModifiers.Control or KeyModifiers.Meta)
        { session.Restart(); e.Handled = true; return; }
        if (e.KeyModifiers != KeyModifiers.None) return;
        if (e.Key == Avalonia.Input.Key.R) { session.Restart(); e.Handled = true; return; }
        var direction = e.Key switch
        {
            Avalonia.Input.Key.Left or Avalonia.Input.Key.A or Avalonia.Input.Key.H => MoveDirection.Left,
            Avalonia.Input.Key.Right or Avalonia.Input.Key.D or Avalonia.Input.Key.L => MoveDirection.Right,
            Avalonia.Input.Key.Up or Avalonia.Input.Key.W or Avalonia.Input.Key.K => MoveDirection.Up,
            Avalonia.Input.Key.Down or Avalonia.Input.Key.S or Avalonia.Input.Key.J => MoveDirection.Down,
            _ => (MoveDirection?)null
        };
        if (direction is {} move) { session.Move(move); e.Handled = true; }
    }
    [Component]
    private void Build(UiContext cx)
    {
        var game = session.Game;
        Ui.Column(cx).Width(500).Height(790).Gap(20).Children(() =>
        {
            Ui.Grid(cx, "*,Auto").Height(90).Children(() =>
            {
                Ui.Text(cx, "2048").FontSize(80).Weight(FontWeight.Bold).TextColor(GameLook.Ink)
                    .Vertical(VerticalAlignment.Center);
                Ui.Row(cx).Cell(0, 1).Gap(5).Vertical(VerticalAlignment.Center).Children(() =>
                {
                    Score(cx, "SCORE", game.Score); Score(cx, "BEST", game.Best);
                });
            });
            Ui.Grid(cx, "*,Auto").Set(Grid.ColumnSpacingProperty, 16d).Height(48).Children(() =>
            {
                Ui.Control(cx, () => new TextBlock
                {
                    Inlines = { new Run("Slide, combine, and reach the "), new Run("2048 tile.") { FontWeight = FontWeight.Bold } }
                }).FontSize(18).TextColor(GameLook.Ink).Wrap().Vertical(VerticalAlignment.Center);
                GameButton(cx, "New Game", session.Restart).Cell(0, 1).Vertical(VerticalAlignment.Center)
                    .Tooltip("New game · R / Ctrl+N / ⌘N");
            });
            Ui.Control(cx, () => new GameBoardCanvas(session)).Width(500).Height(500).Background(GameLook.Board).Radius(6).Children(() =>
            {
                for (var cell = 0; cell < 16; cell++)
                {
                    var index = cell;
                    cx.Scope("cell-" + index, () => Ui.Column(cx).Width(GameLook.TileSize).Height(GameLook.TileSize)
                        .Radius(3).Background("#59EEE4DA")
                        .SetOuter(Canvas.LeftProperty, GameLook.Gap + index % 4 * GameLook.Step)
                        .SetOuter(Canvas.TopProperty, GameLook.Gap + index / 4 * GameLook.Step)
                        .SetOuter(InputElement.IsHitTestVisibleProperty, false));
                }
                foreach (var tile in game.Ghosts) Tile(cx, tile);
                foreach (var tile in game.Tiles) Tile(cx, tile);
                if (game.Stopped)
                    Ui.Control(cx, () => new GameMessage(session)).Width(500).Height(500).SetOuter(Visual.ZIndexProperty, 100);
            }).Configure<GameBoardCanvas>(value => board = value);
            Ui.Column(cx).Height(92).Gap(6).Children(() =>
            {
                Ui.Control(cx, () => new TextBlock
                {
                    Inlines = { new Run("How to play: ") { FontWeight = FontWeight.Bold }, new Run("Move with the arrow keys or swipe the board. Matching tiles combine. Make 2048 to win.") }
                }).FontSize(18).Set(TextBlock.LineHeightProperty, 27d).TextColor(GameLook.Ink).Wrap();
                if (session.SaveError is {} error) Ui.Text(cx, "Progress could not be saved").FontSize(12)
                    .TextColor("#A63838").Tooltip(error);
            });
        });
    }
    private static Element GameButton(UiContext cx, string text, Action action) => Ui.Button(cx, text).Class("game2048").OnClick(action);
    private void Score(UiContext cx, string label, long score)
    {
        cx.Scope(label, () => Ui.Grid(cx, "*").MinWidth(92).Padding(16, 8).Radius(3).Background(GameLook.Board).Children(() =>
        {
            Ui.Column(cx).Gap(0).Center().Children(() =>
            {
                Ui.Text(cx, label).FontSize(13).Weight(FontWeight.Bold).TextColor("#EEE4DA");
                Ui.Text(cx, score.ToString()).FontSize(25).Weight(FontWeight.Bold).TextColor("#FFFFFF").Label(label + " " + score);
            });
            if (label == "SCORE" && session.Game.LastGain > 0 && !session.ReducedMotion)
                cx.Scope("gain-" + session.Game.MoveCount, () => Ui.Control(cx, () => new ScoreRise(session.Game.LastGain)).SetOuter(Visual.ZIndexProperty, 1));
        }));
    }
    private void Tile(UiContext cx, NumberTile tile)
    {
        cx.Scope(tile.Id, () => Ui.Control(cx, () => new NumberTileControl(tile, session.ReducedMotion)).Width(GameLook.TileSize).Height(GameLook.TileSize)
            .SetOuter(Canvas.LeftProperty, GameLook.Gap).SetOuter(Canvas.TopProperty, GameLook.Gap)
            .Set(Visual.RenderTransformProperty, GameLook.Positions[tile.Row * 4 + tile.Column])
            .Label($"{tile.Value}, row {tile.Row + 1}, column {tile.Column + 1}"));
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; session.Changed -= Changed; board?.CancelGesture(); view.Dispose();
    }

    private sealed class GameMessage : UserControl, IDisposable
    {
        private readonly UiView content;
        private readonly bool reducedMotion;
        private readonly CancellationTokenSource animation = new();
        private bool disposed;
        public GameMessage(Game2048Session session)
        {
            var won = !session.Game.Over; reducedMotion = session.ReducedMotion;
            content = new UiView(cx => Ui.Column(cx).Fill().Center().Gap(40).Children(() =>
            {
                Ui.Text(cx, won ? "You win!" : "Game over!").FontSize(60).Weight(FontWeight.Bold).TextColor(won ? GameLook.Light : GameLook.Ink)
                    .Label(won ? "You made the 2048 tile" : $"Game over. Score {session.Game.Score}")
                    .Set(AutomationProperties.LiveSettingProperty, AutomationLiveSetting.Polite);
                Ui.Row(cx).Gap(9).Horizontal(HorizontalAlignment.Center).Children(() =>
                {
                    if (won) GameButton(cx, "Keep going", session.Continue);
                    GameButton(cx, "Try again", session.Restart);
                });
            }));
            Content = new Border { Background = Brush.Parse(won ? "#80EDC22E" : "#BAEEE4DA"), CornerRadius = new CornerRadius(6), Child = content };
            HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Stretch;
            Opacity = 1;
        }
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            if (reducedMotion) return;
            _ = GameLook.Play(new Animation
            {
                Duration = TimeSpan.FromMilliseconds(800), Delay = TimeSpan.FromMilliseconds(1200), FillMode = FillMode.Backward,
                Children = { new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 0d) } }, new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 1d) } } }
            }, this, animation.Token);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; animation.Cancel(); animation.Dispose(); content.Dispose();
        }
    }
    private sealed class ScoreRise : TextBlock, IDisposable
    {
        private readonly CancellationTokenSource animation = new();
        private bool disposed;
        public ScoreRise(long value)
        {
            Text = "+" + value; FontSize = 25; FontWeight = FontWeight.Bold; Foreground = GameLook.InkBrush;
            HorizontalAlignment = HorizontalAlignment.Center; IsHitTestVisible = false; Opacity = 0;
            RenderTransform = new TranslateTransform();
        }
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _ = GameLook.Play(new Animation
            {
                Duration = TimeSpan.FromMilliseconds(600), Easing = new CubicEaseIn(),
                Children = { new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 1d) } }, new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 0d) } } }
            }, this, animation.Token);
            _ = GameLook.Play(new Animation
            {
                Duration = TimeSpan.FromMilliseconds(600), Easing = new CubicEaseIn(),
                Children = { new KeyFrame { Cue = new Cue(0), Setters = { new Setter(TranslateTransform.YProperty, 0d) } }, new KeyFrame { Cue = new Cue(1), Setters = { new Setter(TranslateTransform.YProperty, -75d) } } }
            }, this, animation.Token);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; animation.Cancel(); animation.Dispose();
        }
    }
}
