using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MySharp;

internal static class WorkspaceInputTests
{
    internal static int Run()
    {
        foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark }) Check(variant);
        Console.WriteLine("PASS workspace navigation edge hit targets and task label alignment/clicks in light and dark");
        return 1;
    }

    private static void Check(ThemeVariant variant)
    {
        var app = Application.Current!;
        var fluent = MySharpTheme.CreateFluentBase();
        var theme = new MySharpTheme();
        app.Styles.Add(fluent); app.Styles.Add(theme);
        using var workspace = new Workspace(dark: variant == ThemeVariant.Dark);
        using var view = new UiView(workspace.Build);
        var window = new Window { Width = 1120, Height = 820, RequestedThemeVariant = variant, Content = view };
        void Pump() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
        void Assert(bool value, string message) { if (!value) throw new Exception(message); }
        T Find<T>(Func<T, bool> match) where T : Control => view.GetLogicalDescendants().OfType<T>().Single(match);
        void Click(Control control, Point local)
        {
            var point = control.TranslatePoint(local, window)!.Value;
            window.MouseMove(point, RawInputModifiers.None);
            window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(point, MouseButton.Left, RawInputModifiers.None); Pump();
        }
        void Navigate(string label, bool bottom)
        {
            var button = Find<Button>(b => Equals(b.Content, label));
            var box = (Border)button.Parent!;
            Assert(Math.Abs(button.Bounds.Width - box.Bounds.Width) < 1 && Math.Abs(button.Bounds.Height - 42) < 1,
                "Navigation hit target must fill the painted 42px row, not shrink to its label.");
            // Deliberately outside the label: near the row's right and top/bottom edges.
            Click(box, new Point(box.Bounds.Width - 2, bottom ? 40 : 2));
            Assert(button.Classes.Contains("selected"), $"Clicking the edge must select {label}.");
        }
        try
        {
            window.Show(); Pump();
            Navigate("Tasks", false);
            Assert(view.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Make room for the next step."), "Edge click must navigate to Tasks.");
            var task = Find<CheckBox>(c => Equals(c.Content, "Build the component gallery"));
            var indicator = task.GetVisualDescendants().OfType<Border>().Single(c => c.Name == "NormalRectangle");
            var label = task.GetVisualDescendants().OfType<ContentPresenter>().Single(c => c.Name == "PART_ContentPresenter");
            var indicatorCenter = indicator.TranslatePoint(new Rect(indicator.Bounds.Size).Center, window)!.Value;
            var labelCenter = label.TranslatePoint(new Rect(label.Bounds.Size).Center, window)!.Value;
            Assert(Math.Abs(indicatorCenter.Y - labelCenter.Y) < 1, "Task indicator and label must share a vertical center.");
            Click(label, new Rect(label.Bounds.Size).Center);
            Assert(task.IsChecked == true, "Clicking a task's text must complete it.");
            Click(indicator, new Rect(indicator.Bounds.Size).Center);
            Assert(task.IsChecked == false, "Clicking the indicator must reopen the same task.");
            Navigate("Settings", true);
            Assert(view.GetLogicalDescendants().OfType<TextBox>().Any(t => t.Text == "Alex Chen"), "Bottom edge click must navigate to Settings.");
            Navigate("Overview", false);
        }
        finally { window.Close(); app.Styles.Remove(theme); app.Styles.Remove(fluent); }
    }
}
