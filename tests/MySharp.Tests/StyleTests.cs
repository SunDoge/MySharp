using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using MySharp;

internal static class StyleTests
{
    internal static int Run()
    {
        var useClass = true;
        var useOverride = false;
        Button? button = null;
        using var view = new UiView(cx =>
        {
            var element = Ui.Button(cx, "Theme").Class("notice", useClass);
            if (useOverride) element.Set(Button.ForegroundProperty, Brushes.Green);
            button = element.Native<Button>();
        });
        var window = new Window { Content = view, RequestedThemeVariant = ThemeVariant.Light };
        window.Resources.ThemeDictionaries[ThemeVariant.Light] = new ResourceDictionary { ["accent"] = Brushes.Red };
        window.Resources.ThemeDictionaries[ThemeVariant.Dark] = new ResourceDictionary { ["accent"] = Brushes.Blue };
        window.Styles.Add(new Style(selector => selector.OfType<Button>().Class("notice"))
        {
            Setters = { new Setter(Button.ForegroundProperty, new DynamicResourceExtension("accent")) }
        });
        void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        void Pump() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
        try
        {
            window.Show(); Pump();
            Assert(Equals(button!.Foreground, Brushes.Red), "Native class selector resolves the light theme resource.");
            window.RequestedThemeVariant = ThemeVariant.Dark; Pump();
            Assert(Equals(button.Foreground, Brushes.Blue), "Live theme change updates an existing retained control.");
            useOverride = true; view.Rebuild(); Pump();
            Assert(Equals(button.Foreground, Brushes.Green), "Declared local value overrides style.");
            useOverride = false; view.Rebuild(); Pump();
            Assert(Equals(button.Foreground, Brushes.Blue), "Omission restores current theme style without recreating the control.");
            useClass = false; view.Rebuild(); Pump();
            Assert(!button.Classes.Contains("notice") && !Equals(button.Foreground, Brushes.Blue), "Removing class detaches its selector.");
            useClass = true; view.Rebuild(); Pump();
            Assert(Equals(button.Foreground, Brushes.Blue), "Reintroduced class resolves the latest theme.");
            window.RequestedThemeVariant = ThemeVariant.Light; Pump();
            Assert(Equals(button.Foreground, Brushes.Red), "Switching back updates dynamic resources.");
        }
        finally { window.Close(); }
        Console.WriteLine("PASS retained controls support live theme resources selectors and omitted local overrides");
        return 1;
    }
}
