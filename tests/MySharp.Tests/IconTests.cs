using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucide.Avalonia;
using MySharp;

internal static class IconTests
{
    internal static int Run()
    {
        var app = Application.Current!;
        var fluent = MySharpTheme.CreateFluentBase();
        var theme = new MySharpTheme();
        app.Styles.Add(fluent); app.Styles.Add(theme);
        Button? action = null, tool = null;
        LucideIcon? standalone = null;
        var kind = Icons.Settings;
        var caption = "Settings";
        var showIcon = true;
        var clicks = 0;
        using var view = new UiView(cx => Ui.Column(cx).Gap(8).Children(() =>
        {
            standalone = Ui.Icon(cx, Icons.Home).TextColor("#16A34A").Native<LucideIcon>();
            var button = Ui.Button(cx, caption).Primary().OnClick(() => clicks++);
            if (showIcon) button.Icon(kind);
            action = button.Native<Button>();
            tool = Ui.IconButton(cx, Icons.Trash, "Delete task").Class("quiet").OnClick(() => clicks++).Native<Button>();
        }));
        var window = new Window { Width = 400, Height = 300, Content = view, RequestedThemeVariant = ThemeVariant.Light };
        void Pump() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
        void Assert(bool value, string message) { if (!value) throw new Exception(message); }
        LucideIcon Icon(Button button) => button.GetVisualDescendants().OfType<LucideIcon>().Single();
        bool ColorIs(IBrush? brush, string value) => brush is ISolidColorBrush solid && solid.Color == Color.Parse(value);
        try
        {
            window.Show(); Pump();
            Assert(standalone!.Size == 16 && ColorIs(standalone.Foreground, "#16A34A"), "Standalone icon accepts size and foreground.");
            var icon = Icon(action!);
            Assert(Equals(action!.Content, "Settings") && icon.Kind == Icons.Settings && icon.Size == 16, "Icon buttons preserve their string content and inherit kind/size.");
            Assert(ColorIs(icon.Foreground, "#FFFFFF"), "Primary icon inherits the native template's label color.");
            var changes = 0;
            icon.PropertyChanged += (_, _) => changes++;
            view.Rebuild(); Pump();
            Assert(ReferenceEquals(icon, Icon(action)) && changes == 0, "An unchanged declaration must retain its icon without rewriting properties.");
            kind = Icons.Home; caption = "Home"; view.Rebuild(); Pump();
            Assert(ReferenceEquals(icon, Icon(action)) && icon.Kind == Icons.Home && Equals(action.Content, "Home"), "Changing kind/text recycles the same native icon.");
            Assert(action.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Home"), "Recycled icon content updates its visible label.");
            Assert(AutomationProperties.GetName(tool!) == "Delete task" && Equals(ToolTip.GetTip(tool!), "Delete task"), "Icon-only controls have accessible names and tooltips.");
            Assert(tool!.Bounds.Width >= 34 && tool.Bounds.Height >= 34, "Icon-only buttons keep a full control-sized hit target.");
            var point = tool.TranslatePoint(new Point(tool.Bounds.Width - 2, tool.Bounds.Height / 2), window)!.Value;
            window.MouseMove(point, RawInputModifiers.None);
            window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(point, MouseButton.Left, RawInputModifiers.None); Pump();
            Assert(clicks == 1, "Clicking icon-button padding activates its normal button event.");
            window.RequestedThemeVariant = ThemeVariant.Dark;
            action.IsEnabled = false; Pump();
            Assert(ColorIs(icon.Foreground, "#A1A1AA"), "Disabled icon follows the live dark disabled label color.");
            showIcon = false; view.Rebuild(); Pump();
            Assert(action.ContentTemplate is null && !action.GetVisualDescendants().OfType<LucideIcon>().Any() && Equals(action.Content, "Home"), "Omitting Icon restores the native text-only button.");
        }
        finally { window.Close(); app.Styles.Remove(theme); app.Styles.Remove(fluent); }
        Console.WriteLine("PASS retained vector icons recycling omission theme inheritance accessibility and padding hit target");
        return 1;
    }
}
