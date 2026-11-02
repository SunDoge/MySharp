using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using MySharp;
using MySharp.Examples.Game2048;

internal static class Game2048Tests
{
    internal static int Run()
    {
        var count = 0;
        void Test(string name, Action test) { test(); count++; Console.WriteLine("PASS 2048 " + name); }
        void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
        Game2048State Game(long[] values)
        {
            var game = new Game2048State(new Random(7));
            var cells = new long[16]; values.CopyTo(cells, 0);
            Check(game.Restore(new(1, 0, 0, cells, false, false)), "Fixture must restore.");
            return game;
        }
        long[] WithoutSpawn(Game2048State game)
        {
            var cells = new long[16];
            foreach (var t in game.Tiles.Where(t => t.Id != game.SpawnedId)) cells[t.Row * 4 + t.Column] = t.Value;
            return cells;
        }
        Test("line compression merges each pair once in all four directions", () =>
        {
            foreach (var direction in Enum.GetValues<MoveDirection>())
            {
                var cells = new long[16];
                for (var i = 0; i < 4; i++) cells[direction is MoveDirection.Up or MoveDirection.Down ? i * 4 : i] = 2;
                var game = Game(cells);
                Check(game.Move(direction) && game.Score == 8 && game.Tiles.Count == 3, "Four twos make two fours and one spawn.");
                var compact = WithoutSpawn(game);
                var destinations = direction switch
                {
                    MoveDirection.Left => new[] { 0, 1 }, MoveDirection.Right => [2, 3],
                    MoveDirection.Up => [0, 4], _ => [8, 12]
                };
                Check(destinations.All(i => compact[i] == 4) && compact.Sum() == 8, "Pairs merge toward the chosen edge.");
            }
            var single = Game([2, 2, 4]); single.Move(MoveDirection.Left);
            Check(WithoutSpawn(single).Take(4).SequenceEqual(new long[] { 4, 4, 0, 0 }) && single.Score == 4, "A newly merged four must not merge again.");
        });
        Test("blocked moves preserve tile IDs and score", () =>
        {
            var game = Game([2, 4, 8]);
            var ids = game.Tiles.Select(t => t.Id).ToArray();
            Check(!game.Move(MoveDirection.Left) && game.Score == 0 && game.MoveCount == 0, "Blocked move must not score or spawn.");
            Check(ids.SequenceEqual(game.Tiles.Select(t => t.Id)), "Blocked move preserves identities.");
        });
        Test("winning pauses movement and can continue beyond 2048", () =>
        {
            var game = Game([1024, 1024]);
            game.Move(MoveDirection.Left);
            Check(game.Won && game.Stopped && game.Score == 2048 && !game.Move(MoveDirection.Right), "Winning pauses the board.");
            Check(game.Continue() && game.Move(MoveDirection.Right) && game.Tiles.Any(t => t.Value == 2048 && t.Column > 0), "Keep going resumes the won board.");
            game.Restart();
            Check(game.Score == 0 && game.Best == 2048 && game.Tiles.Count == 2 && !game.Won, "Restart keeps best score but resets the game.");
        });
        Test("full nonmergeable boards stop and save only their best score", () =>
        {
            var game = Game([0, 2, 4, 8, 8, 16, 32, 64, 128, 256, 512, 1024, 32, 128, 64, 256]);
            Check(game.Move(MoveDirection.Left) && game.Over && game.Stopped, "Last empty cell must detect loss after spawning.");
            Check(!game.Move(MoveDirection.Up) && game.Save().Cells is null, "Lost board no longer moves and next launch starts fresh.");
        });
        Test("atomic persistence restores active game and rejects malformed saves", () =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "mysharp-2048-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "state.json");
            try
            {
                var session = new Game2048Session(path, 2);
                session.Game.Restore(new(1, 0, 0, [2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], false, false));
                session.Move(MoveDirection.Left);
                var saved = File.ReadAllText(path);
                var restored = new Game2048Session(path, 99);
                Check(restored.Game.Score == 4 && restored.Game.Best == 4 && restored.Game.Tiles.Select(t => (t.Value, t.Row, t.Column)).SequenceEqual(session.Game.Tiles.Select(t => (t.Value, t.Row, t.Column))), "Reload retains board and score.");
                restored.Restart();
                Check(new Game2048Session(path).Game.Best == 4 && !Directory.GetFiles(directory, "*.tmp").Any(), "Atomic saves preserve best and clean temporary files.");
                File.WriteAllText(path, "{broken");
                Check(new Game2048Session(path).Game.Tiles.Count == 2, "Corrupt save starts a playable game.");
                var bad = new GameSave(1, 80, 0, [3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], false, false);
                Check(!restored.Game.Restore(bad) && restored.Game.Tiles.Count == 2, "Non-power-of-two cells cannot corrupt a running game.");
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        });
        var app = Application.Current!;
        var fluent = MySharpTheme.CreateFluentBase(); var theme = new MySharpTheme();
        app.Styles.Add(fluent); app.Styles.Add(theme);
        try
        {
            Test("gallery keyboard aliases drag restart and navigation retain game state", () =>
            {
                using var workspace = new Workspace("2048");
                using var view = new UiView(workspace.Build);
                var window = new Window { Width = 1120, Height = 820, Content = view };
                void Pump() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
                try
                {
                    window.Show(); Pump();
                    var gameView = view.GetLogicalDescendants().OfType<Game2048View>().Single();
                    var game = gameView.Session.Game;
                    foreach (var key in new[] { Key.Left, Key.A, Key.H, Key.Right, Key.D, Key.L, Key.Up, Key.W, Key.K, Key.Down, Key.S, Key.J })
                    {
                        var vertical = key is Key.Up or Key.W or Key.K or Key.Down or Key.S or Key.J;
                        game.Restore(new(1, 0, 0, vertical ? [0, 2, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0] : [0, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], false, false));
                        gameView.View.Rebuild(); Pump();
                        var board = view.GetLogicalDescendants().OfType<GameBoardCanvas>().Single(); board.Focus();
                        window.KeyPress(key, RawInputModifiers.None, default, null); window.KeyRelease(key, RawInputModifiers.None, default, null); Pump();
                        Check(game.Score == 4 && game.Tiles.Any(t => t.Value == 4 && (vertical
                            ? t.Row == (key is Key.Down or Key.S or Key.J ? 3 : 0)
                            : t.Column == (key is Key.Right or Key.D or Key.L ? 3 : 0))), "Native keyboard aliases move the actual board.");
                    }
                    game.Restore(new(1, 0, 0, [0, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], false, false)); gameView.View.Rebuild(); Pump();
                    var boardControl = view.GetLogicalDescendants().OfType<GameBoardCanvas>().Single();
                    var start = boardControl.TranslatePoint(new Point(300, 250), window)!.Value;
                    var end = boardControl.TranslatePoint(new Point(100, 250), window)!.Value;
                    var moves = game.MoveCount;
                    window.MouseDown(start, MouseButton.Left); window.MouseMove(end, RawInputModifiers.LeftMouseButton); window.MouseUp(end, MouseButton.Left); Pump();
                    Check(game.MoveCount == moves + 1, "Scaled native pointer drag makes exactly one move.");
                    var before = game.Tiles.Select(t => t.Id).ToArray();
                    window.KeyPress(Key.R, RawInputModifiers.Control, default, null); window.KeyRelease(Key.R, RawInputModifiers.Control, default, null); Pump();
                    Check(before.SequenceEqual(game.Tiles.Select(t => t.Id)), "Unrelated modified shortcuts do not reset the game.");
                    window.KeyPress(Key.R, RawInputModifiers.None, default, null); window.KeyRelease(Key.R, RawInputModifiers.None, default, null); Pump();
                    Check(game.Score == 0 && game.Tiles.Count == 2, "R resets through native input.");
                    foreach (var modifier in new[] { RawInputModifiers.Control, RawInputModifiers.Meta })
                    {
                        var oldIds = game.Tiles.Select(t => t.Id).ToArray();
                        window.KeyPress(Key.N, modifier, default, null); window.KeyRelease(Key.N, modifier, default, null); Pump();
                        Check(!oldIds.SequenceEqual(game.Tiles.Select(t => t.Id)), "Ctrl/Command+N starts a new game.");
                    }
                    var buttons = view.GetLogicalDescendants().OfType<Button>();
                    buttons.Single(b => Equals(b.Content, "Overview")).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Pump();
                    view.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "2048")).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Pump();
                    Check(ReferenceEquals(game, view.GetLogicalDescendants().OfType<Game2048View>().Single().Session.Game), "Leaving the page cancels visuals but retains the session.");
                    window.Width = 960; window.Height = 680; Pump();
                    var boardBounds = view.GetLogicalDescendants().OfType<GameBoardCanvas>().Single();
                    var top = boardBounds.TranslatePoint(default, window)!.Value;
                    var bottom = boardBounds.TranslatePoint(new Point(500, 500), window)!.Value;
                    Check(top.X >= 216 && bottom.X <= 960 && top.Y >= 0 && bottom.Y <= 680, "Board fits the minimum gallery viewport.");
                    window.Width = 2200; window.Height = 1600; Pump();
                    top = boardBounds.TranslatePoint(default, window)!.Value;
                    bottom = boardBounds.TranslatePoint(new Point(500, 500), window)!.Value;
                    Check(bottom.X - top.X <= 801, "Large windows cap the reference game's scale at 1.6x.");
                }
                finally { window.Close(); }
            });
            Test("win and loss overlays expose native buttons and restore board focus", () =>
            {
                var session = new Game2048Session();
                session.Game.Restore(new(1, 0, 0, [1024, 1024, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], false, false));
                using var game = new Game2048View(session);
                var window = new Window { Width = 650, Height = 850, Content = game };
                void Pump() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
                void Click(string label)
                {
                    var button = game.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, label));
                    var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
                    window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left); Pump();
                }
                try
                {
                    window.Show(); Pump(); session.Move(MoveDirection.Left); Pump();
                    Check(game.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "You win!"), "Win message is mounted over the real board.");
                    var cells = session.Game.Save().Cells!;
                    window.KeyPress(Key.Right, RawInputModifiers.None, default, null); window.KeyRelease(Key.Right, RawInputModifiers.None, default, null); Pump();
                    Check(cells.SequenceEqual(session.Game.Save().Cells!), "Movement is blocked under the winning overlay.");
                    Click("Keep going");
                    Check(session.Game.KeepGoing && game.GetLogicalDescendants().OfType<GameBoardCanvas>().Single().IsFocused, "Keep going returns keyboard focus to the board.");
                    window.KeyPress(Key.Right, RawInputModifiers.None, default, null); window.KeyRelease(Key.Right, RawInputModifiers.None, default, null); Pump();
                    Check(session.Game.Tiles.Any(t => t.Value == 2048 && t.Column > 0), "Keyboard immediately works after dismissing the overlay.");
                    session.Game.Restore(new(1, session.Game.Best, 0, [0, 2, 4, 8, 8, 16, 32, 64, 128, 256, 512, 1024, 32, 128, 64, 256], false, false));
                    session.Move(MoveDirection.Left); Pump();
                    Check(game.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Game over!"), "Loss message replaces the board interaction.");
                    Check(!game.GetLogicalDescendants().OfType<Button>().Any(b => Equals(b.Content, "Keep going")), "Game over only offers restart.");
                    Click("Try again");
                    Check(!session.Game.Stopped && session.Game.Score == 0 && session.Game.Tiles.Count == 2, "Try again creates a playable board.");
                }
                finally { window.Close(); }
            });
            Test("wheel gestures consume momentum once and pointer cancellation releases capture", () =>
            {
                var session = new Game2048Session();
                session.Game.Restore(new(1, 0, 0, [0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], false, false));
                using var game = new Game2048View(session);
                var window = new Window { Width = 650, Height = 850, Content = game };
                void Pump() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
                try
                {
                    window.Show(); Pump();
                    var board = game.GetLogicalDescendants().OfType<GameBoardCanvas>().Single();
                    var point = board.TranslatePoint(new Point(250, 250), window)!.Value;
                    for (var i = 0; i < 15; i++) { window.MouseWheel(point, new Vector(1, 0)); Pump(); }
                    Check(session.Game.MoveCount == 1, "Momentum events in one scroll gesture must not add repeated tiles.");
                    Thread.Sleep(210); window.MouseWheel(point, new Vector(-1, 0)); Pump();
                    Check(session.Game.MoveCount == 2, "A fresh gesture after finger rest moves again.");
                    window.MouseDown(point, MouseButton.Left); window.MouseMove(point + new Vector(80, 0), RawInputModifiers.LeftMouseButton);
                    window.KeyPress(Key.Escape, RawInputModifiers.None, default, null); window.KeyRelease(Key.Escape, RawInputModifiers.None, default, null);
                    window.MouseUp(point + new Vector(80, 0), MouseButton.Left); Pump();
                    Check(session.Game.MoveCount == 2, "Escape cancels an in-flight swipe.");
                    window.MouseDown(point, MouseButton.Left); window.Width -= 40; Pump();
                    window.MouseUp(point + new Vector(80, 0), MouseButton.Left); Pump();
                    Check(session.Game.MoveCount == 2, "Resizing cancels the captured swipe instead of using a changed coordinate transform.");
                    var reset = game.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "New Game"));
                    var buttonPoint = reset.TranslatePoint(new Point(reset.Bounds.Width / 2, reset.Bounds.Height / 2), window)!.Value;
                    window.MouseDown(buttonPoint, MouseButton.Left); window.MouseUp(buttonPoint, MouseButton.Left); Pump();
                    Check(session.Game.MoveCount == 0 && session.Game.Tiles.Count == 2, "Canceled board capture must not swallow the New Game button.");
                }
                finally { window.Close(); }
            });
            Test("reduced motion places tiles directly without transitions", () =>
            {
                var session = new Game2048Session(reducedMotion: true);
                session.Game.Restore(new(1, 0, 0, [2, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], false, false));
                using var game = new Game2048View(session);
                var window = new Window { Width = 650, Height = 850, Content = game };
                try
                {
                    window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                    var tile = game.GetLogicalDescendants().OfType<NumberTileControl>().First(t => t.Value == 2);
                    session.Move(MoveDirection.Right); Dispatcher.UIThread.RunJobs();
                    Check((tile.Transitions is null || tile.Transitions.Count == 0) && Math.Abs(tile.RenderTransform!.Value.M31 - 363.75) < 1,
                        "Reduced motion disables slide and pop delays.");
                }
                finally { window.Close(); }
            });
            Test("native slide animations advance without rebuilding declarations", () =>
            {
                var session = new Game2048Session();
                session.Game.Restore(new(1, 0, 0, [2, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], false, false));
                using var game = new Game2048View(session);
                var window = new Window { Width = 650, Height = 850, Content = game };
                try
                {
                    window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                    var tile = game.GetLogicalDescendants().OfType<NumberTileControl>().First(t => t.Value == 2);
                    game.View.ProfilingEnabled = true;
                    var builds = 0; game.View.FrameMeasured += _ => builds++;
                    session.Move(MoveDirection.Right);
                    var sawBetween = false;
                    var watch = Stopwatch.StartNew();
                    while (watch.ElapsedMilliseconds < 450)
                    {
                        AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
                        var x = tile.RenderTransform!.Value.M31;
                        sawBetween |= x > 0 && x < 363;
                        Thread.Sleep(3);
                    }
                    Check(sawBetween && Math.Abs(tile.RenderTransform!.Value.M31 - 363.75) < 1, "Native transform must slide then settle.");
                    Check(builds == 1, "Animation frames do not rebuild the MySharp component tree.");
                }
                finally { window.Close(); }
            });
        }
        finally { app.Styles.Remove(theme); app.Styles.Remove(fluent); }
        return count;
    }
}
