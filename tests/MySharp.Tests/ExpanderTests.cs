using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MySharp;

internal static class ExpanderTests
{
    internal static int Run()
    {
        var app = Application.Current!;
        var fluent = MySharpTheme.CreateFluentBase();
        var theme = new MySharpTheme();
        app.Styles.Add(fluent); app.Styles.Add(theme);
        foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var expanded = false;
            var changes = 0;
            var text = "draft";
            Expander? section = null;
            TextBox? editor = null;
            using var view = new UiView(cx =>
                section = Ui.Expander(cx, "Details", expanded, value => { expanded = value; changes++; })
                    .Children(() => editor = Ui.TextInput(cx, text, value => text = value).Native<TextBox>()).Native<Expander>());
            var window = new Window { Width = 400, Height = 240, Content = view, RequestedThemeVariant = variant };
            void Pump() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
            void Assert(bool value, string message) { if (!value) throw new Exception(message); }
            try
            {
                window.Show(); Pump();
                var header = section!.GetVisualDescendants().OfType<ToggleButton>().Single();
                Assert(section.Bounds.Width >= 398 && header.Bounds.Width >= 398, "Default disclosure header must fill the available row.");
                Assert(Math.Abs(header.Bounds.Height - 36) < 1, "Disclosure header uses the compact 36px default metric.");
                window.MouseMove(new Point(399, 239), RawInputModifiers.None); Pump();
                var face = header.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "ToggleButtonBackground");
                var background = (ISolidColorBrush)face.Background!;
                Assert(background.Color == Color.Parse(variant == ThemeVariant.Dark ? "#18181B" : "#FFFFFF"), "Disclosure header follows the shared neutral surface instead of Fluent's unrelated default.");
                var point = header.TranslatePoint(new Point(header.Bounds.Width - 2, header.Bounds.Height / 2), window)!.Value;
                window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
                window.MouseUp(point, MouseButton.Left, RawInputModifiers.None); Pump();
                Assert(expanded && changes == 1, "Disclosure row edge opens the content exactly once.");
                var retained = editor;
                editor!.Text = "edited"; Pump();
                header.Focus();
                window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
                window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None); Pump();
                Assert(!expanded && changes == 2, "Native Space closes a focused disclosure.");
                window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
                window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None); Pump();
                Assert(expanded && changes == 3 && ReferenceEquals(retained, editor) && editor.Text == "edited", "Reopening retains edited content and native control identity.");
            }
            finally { window.Close(); }
        }
        app.Styles.Remove(theme); app.Styles.Remove(fluent);
        Console.WriteLine("PASS default disclosure full-row pointer keyboard and retained content in light and dark");
        return 1;
    }
}
