using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;
using MySharp;

internal static class ThemeTests
{
    internal static int Run()
    {
        var fluent = MySharpTheme.CreateFluentBase();
        Avalonia.Application.Current!.Styles.Add(fluent);
        var theme = new MySharpTheme();
        Avalonia.Application.Current!.Styles.Add(theme);
        Button? normal = null, primary = null, quiet = null;
        ListBox? list = null;
        var selectedTool = false;
        var items = new[] { "One", "Two" };
        TextBox? input = null;
        using var view = new UiView(cx => Ui.Column(cx).Children(() =>
        {
            normal = Ui.Button(cx, "Normal").Native<Button>();
            primary = Ui.Button(cx, "Primary").Primary().Native<Button>();
            input = Ui.TextInput(cx, "", _ => { }).Native<TextBox>();
            quiet = Ui.Button(cx, "Tool").Class("quiet").Class("selected", selectedTool).Native<Button>();
            list = Ui.List(cx, items, 0, _ => { }).Native<ListBox>();
        }));
        var window = new Window { Width = 400, Height = 300, Content = view, RequestedThemeVariant = ThemeVariant.Light };

        void Assert(bool value, string message) { if (!value) throw new Exception(message); }
        bool ColorIs(IBrush? brush, string hex) => brush is ISolidColorBrush solid && solid.Color == Color.Parse(hex);
        void Pump() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
        try
        {
            window.Show(); Pump();
            Assert(ColorIs(normal!.Background, "#F4F4F5"), $"Neutral button palette must replace default Fluent gray; got {normal.Background}.");
            Assert(ColorIs(primary!.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter").Background, "#2563EB"), "Accent palette must replace default Fluent cyan.");
            Assert(normal.CornerRadius.TopLeft == 6 && normal.Padding.Left == 12 && input!.MinHeight == 36, "Shared control metrics apply.");
            Assert(quiet!.Background is ISolidColorBrush transparent && transparent.Color.A == 0 && quiet.MinHeight == 30, "Quiet tools have no persistent button face.");
            var item = list!.GetVisualDescendants().OfType<ListBoxItem>().First(i => i.IsSelected);
            var presenter = item.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");
            Assert(ColorIs(presenter.Background, "#EAF0FF") && presenter.BorderThickness.Left == 2, "Selected list rows use a soft surface and explicit leading marker.");
            selectedTool = true; view.Rebuild(); Pump();
            Assert(ColorIs(quiet.Background, "#EAF0FF") && ColorIs(quiet.Foreground, "#1D4ED8"), "Selected quiet tool remains distinct from primary actions.");
            var toolPoint = quiet.TranslatePoint(new Point(5, 5), window)!.Value;
            window.MouseMove(toolPoint, RawInputModifiers.None); Pump();
            var toolPresenter = quiet.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");
            Assert(ColorIs(toolPresenter.Background, "#DCE7FF") && toolPresenter.BorderBrush is ISolidColorBrush toolBorder && toolBorder.Color.A == 0, "Selected toolbar hover keeps a soft face without a button outline.");
            window.MouseDown(toolPoint, MouseButton.Left, RawInputModifiers.None); Pump();
            Assert(ColorIs(toolPresenter.Background, "#CEDCFA"), "Pressed toolbar selection uses its own state.");
            window.MouseUp(toolPoint, MouseButton.Left, RawInputModifiers.None); Pump();
            var point = normal.TranslatePoint(new Avalonia.Point(5, 5), window)!.Value;
            window.MouseMove(point, RawInputModifiers.None); Pump();
            Assert(ColorIs(normal.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter").Background, "#E9E9EC"), "Pointer hover must use the MyGo-inspired surface state.");
            window.MouseMove(new Avalonia.Point(399, 299), RawInputModifiers.None); Pump();
            window.RequestedThemeVariant = ThemeVariant.Dark; Pump();
            Assert(ColorIs(normal.Background, "#27272A"), "Native theme switch updates neutral controls.");
            Assert(ColorIs(quiet.Background, "#253552") && ColorIs(presenter.Background, "#253552"), "Selected surfaces track native dark theme changes.");
            Assert(ColorIs(input!.Background, "#18181B"), "Native theme switch updates input backgrounds.");
            normal.IsEnabled = false; Pump();
            Assert(ColorIs(normal.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter").Foreground, "#A1A1AA"), "Disabled controls retain readable muted text.");
        }
        finally { window.Close(); Avalonia.Application.Current!.Styles.Remove(theme); Avalonia.Application.Current.Styles.Remove(fluent); }
        Console.WriteLine("PASS default MySharp theme metrics palettes live variants and disabled state");
        return 1 + ControlStates();
    }

    private static int ControlStates()
    {
        var app = Avalonia.Application.Current!;
        var fluent = MySharpTheme.CreateFluentBase();
        var theme = new MySharpTheme();
        app.Styles.Add(fluent); app.Styles.Add(theme);
        var check = new CheckBox { Content = "Include text", IsChecked = true };
        var toggle = new ToggleSwitch { Content = "Automatic processing", IsChecked = true };
        var select = new ComboBox { ItemsSource = new[] { "PNG", "JPEG" }, SelectedIndex = 0 };
        Border? popupBorder = null;
        select.TemplateApplied += (_, e) => popupBorder = e.NameScope.Find<Border>("PopupBorder");
        var window = new Window
        {
            Width = 400, Height = 300, RequestedThemeVariant = ThemeVariant.Light,
            Content = new StackPanel { Spacing = 12, Children = { check, toggle, select } }
        };
        void Pump() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
        void Assert(bool value, string message) { if (!value) throw new Exception(message); }
        bool ColorIs(IBrush? brush, string hex) => brush is ISolidColorBrush solid && solid.Color == Color.Parse(hex);
        T Part<T>(Control owner, string name) where T : Control => owner.GetVisualDescendants().OfType<T>().Single(x => x.Name == name);
        try
        {
            window.Show(); Pump();
            var box = Part<Border>(check, "NormalRectangle");
            var track = Part<Border>(toggle, "SwitchKnobBounds");
            Assert(box.Width == 20 && box.CornerRadius.TopLeft == 4 && ColorIs(box.Background, "#2563EB"), $"Check indicator uses compact geometry and the shared accent: {box.Width}, {box.CornerRadius}, {box.Background}.");
            Assert(ColorIs(track.Background, "#2563EB"), "Switch uses the shared accent.");
            var label = Part<ContentPresenter>(toggle, "PART_ContentPresenter");
            Assert(Math.Abs(label.Bounds.Center.Y - track.Bounds.Center.Y) < 1 && label.Bounds.X >= 40, "Switch label is inline with its track.");
            var point = toggle.TranslatePoint(new Point(4, 11), window)!.Value;
            window.MouseMove(point, RawInputModifiers.None); Pump();
            Assert(ColorIs(track.Background, "#1D4ED8"), "Inline switch retains native hover feedback.");
            window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(point, MouseButton.Left, RawInputModifiers.None); Pump();
            Assert(toggle.IsChecked == false, "Inline switch retains native pointer toggling.");
            toggle.Focus();
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None); Pump();
            Assert(toggle.IsChecked == true, "Inline switch retains native keyboard toggling.");
            // Exercise Avalonia's native knob dragging through the required named parts.
            toggle.IsChecked = false; Pump();
            var knob = Part<Grid>(toggle, "PART_MovingKnobs");
            var start = knob.TranslatePoint(new Point(11, 11), window)!.Value;
            window.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
            window.MouseMove(start + new Vector(18, 0), RawInputModifiers.LeftMouseButton);
            window.MouseUp(start + new Vector(18, 0), MouseButton.Left, RawInputModifiers.None); Pump();
            Assert(toggle.IsChecked == true, "Inline switch retains native knob dragging.");
            window.MouseMove(new Point(399, 299), RawInputModifiers.None); Pump();
            window.RequestedThemeVariant = ThemeVariant.Dark; Pump();
            Assert(ColorIs(box.Background, "#3B82F6") && ColorIs(track.Background, "#3B82F6"), "Checked controls update their accent on a live dark theme switch.");
            select.IsDropDownOpen = true; Pump();
            Assert(ColorIs(popupBorder?.Background, "#18181B"), "Dropdown popup inherits the live dark surface.");
            select.IsDropDownOpen = false;
            check.IsEnabled = false; toggle.IsEnabled = false; Pump();
            Assert(ColorIs(box.Background, "#3F3F46") && ColorIs(track.Background, "#3F3F46"), $"Disabled checked controls use neutral surfaces: {box.Background}, {track.Background}.");
        }
        finally { window.Close(); app.Styles.Remove(theme); app.Styles.Remove(fluent); }
        Console.WriteLine("PASS shared control states inline switch pointer keyboard drag and dark dropdown");
        return 1;
    }
}
