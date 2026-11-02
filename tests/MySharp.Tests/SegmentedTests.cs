using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MySharp;

internal static class SegmentedTests
{
    internal static int Run()
    {
        var app = Application.Current!;
        var fluent = MySharpTheme.CreateFluentBase();
        var theme = new MySharpTheme();
        app.Styles.Add(fluent); app.Styles.Add(theme);
        ListBox? group = null;
        var values = new[] { "Original", "Translated", "Both" };
        var selected = 0;
        var changes = 0;
        using var view = new UiView(cx => group = Ui.Segmented(cx, values, selected, value => { selected = value; changes++; }).Label("Preview").Native<ListBox>());
        var window = new Window { Width = 460, Height = 160, Content = view, RequestedThemeVariant = ThemeVariant.Light };
        void Pump() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
        void Assert(bool value, string message) { if (!value) throw new Exception(message); }
        bool ColorIs(IBrush? brush, string color) => brush is ISolidColorBrush solid && solid.Color == Color.Parse(color);
        ContentPresenter Face(ListBoxItem item) => item.GetVisualDescendants().OfType<ContentPresenter>().Single(p => p.Name == "PART_ContentPresenter");
        try
        {
            window.Show(); Pump();
            var first = (ListBoxItem)group!.ContainerFromIndex(0)!;
            var second = (ListBoxItem)group.ContainerFromIndex(1)!;
            Assert(second.Bounds.X > first.Bounds.X && second.Bounds.Y == first.Bounds.Y, "Segments must share a horizontal row.");
            Assert(ColorIs(Face(first).Background, "#FFFFFF") && Face(first).BorderThickness.Left == 0, "Selected segment has a neutral raised surface without a list selection stripe.");
            var point = second.TranslatePoint(new Point(second.Bounds.Width - 2, second.Bounds.Height / 2), window)!.Value;
            window.MouseMove(point, RawInputModifiers.None); Pump();
            Assert(ColorIs(Face(second).Background, "#E9E9EC"), "Unselected segment has neutral hover feedback.");
            window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(point, MouseButton.Left, RawInputModifiers.None); Pump();
            Assert(selected == 1 && changes == 1 && ColorIs(Face(second).Background, "#FFFFFF"), "Segment padding selects exactly once and keeps a neutral selected face.");
            view.Rebuild(); Pump();
            Assert(ReferenceEquals(second, group.ContainerFromIndex(1)) && changes == 1, "Unchanged declarations retain native segment containers and do not repeat callbacks.");
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None); Pump();
            Assert(selected == 2 && changes == 2, "Native right-arrow navigation selects the next segment.");
            window.MouseMove(new Point(459, 159), RawInputModifiers.None);
            window.RequestedThemeVariant = ThemeVariant.Dark; Pump();
            var last = (ListBoxItem)group.ContainerFromIndex(2)!;
            Assert(ColorIs(Face(last).Background, "#3F3F46") && ColorIs(Face(last).Foreground, "#F4F4F5"), "Selected segment responds to live dark theme changes with a raised neutral surface.");
            group.IsEnabled = false; Pump();
            Assert(ColorIs(Face(last).Foreground, "#A1A1AA"), "Disabled selected text remains muted.");
            Assert(window.FontFamily.FamilyNames.Any(n => n.Contains("Noto Sans CJK SC")), "Default UI font declares a specific CJK fallback before the system fallback.");
        }
        finally { window.Close(); app.Styles.Remove(theme); app.Styles.Remove(fluent); }
        Console.WriteLine("PASS segmented pointer padding keyboard selection retained containers live variants and UI font fallback");
        return 1;
    }
}
