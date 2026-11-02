using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace MySharp;

/// <summary>Code-only MyGo-inspired palette and metrics over Avalonia's native Fluent templates.</summary>
public sealed partial class MySharpTheme : Styles
{
    public MySharpTheme()
    {
        Resources.ThemeDictionaries[ThemeVariant.Light] = Palette(false);
        Resources.ThemeDictionaries[ThemeVariant.Dark] = Palette(true);
        // Explicit UI fallbacks avoid picking an arbitrary editor/monospace font
        // for CJK when only the Latin family is specified.
        Resources["MySharpFontFamily"] = new FontFamily("fonts:Inter#Inter, PingFang SC, Microsoft YaHei UI, Noto Sans CJK SC, Noto Sans CJK JP");
        Resources["ControlCornerRadius"] = new CornerRadius(6);
        Resources["OverlayCornerRadius"] = new CornerRadius(8);
        Resources["ButtonPadding"] = new Thickness(12, 6);
        Resources["TextControlThemePadding"] = new Thickness(10, 6);
        Resources["TextControlThemeMinHeight"] = 34d;
        Resources["TextControlBorderThemeThickness"] = new Thickness(1);
        Resources["TextControlBorderThemeThicknessFocused"] = new Thickness(1);
        Resources["ButtonBorderThemeThickness"] = new Thickness(1);
        Add(new Style(x => x.OfType<Window>())
        {
            Setters =
            {
                new Setter(TemplatedControl.FontSizeProperty, 14d),
                new Setter(TemplatedControl.FontFamilyProperty, new DynamicResourceExtension("MySharpFontFamily"))
            }
        });
        Add(new Style(x => x.OfType<Button>())
        {
            Setters =
            {
                new Setter(Layoutable.MinHeightProperty, 34d),
                new Setter(TemplatedControl.FontWeightProperty, FontWeight.Normal),
                new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(6)),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(12, 6)),
                new Setter(ContentControl.VerticalContentAlignmentProperty, VerticalAlignment.Center)
            }
        });
        Add(new Style(x => x.OfType<Button>().Class("quiet"))
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent),
                new Setter(TemplatedControl.BorderBrushProperty, Brushes.Transparent),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(9, 5)),
                new Setter(Layoutable.MinHeightProperty, 30d)
            }
        });
        Add(new Style(x => x.OfType<Button>().Class("quiet").Class("selected"))
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, new DynamicResourceExtension("MySharpSelection")),
                new Setter(TemplatedControl.ForegroundProperty, new DynamicResourceExtension("MySharpSelectionText"))
            }
        });
        Add(new Style(x => x.OfType<Button>().Class("quiet").Template().OfType<Avalonia.Controls.Presenters.ContentPresenter>().Name("PART_ContentPresenter"))
        {
            Setters = { new Setter(Avalonia.Controls.Presenters.ContentPresenter.BorderBrushProperty, Brushes.Transparent) }
        });
        foreach (var state in new[] { (Pseudo: ":pointerover", Brush: "MySharpSelectionHover"), (Pseudo: ":pressed", Brush: "MySharpSelectionPressed"), (Pseudo: ":disabled", Brush: "ButtonBackgroundDisabled") })
        {
            Add(new Style(x => x.OfType<Button>().Class("quiet").Class("selected").Class(state.Pseudo).Template().OfType<Avalonia.Controls.Presenters.ContentPresenter>().Name("PART_ContentPresenter"))
            {
                Setters =
                {
                    new Setter(Avalonia.Controls.Presenters.ContentPresenter.BackgroundProperty, new DynamicResourceExtension(state.Brush)),
                    new Setter(Avalonia.Controls.Presenters.ContentPresenter.ForegroundProperty, new DynamicResourceExtension(state.Pseudo == ":disabled" ? "ButtonForegroundDisabled" : "MySharpSelectionText"))
                }
            });
        }
        Add(new Style(x => x.OfType<ListBox>())
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, new DynamicResourceExtension("TextControlBackground")),
                new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(0))
            }
        });
        Add(new Style(x => x.OfType<ListBoxItem>())
        {
            Setters =
            {
                new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(6)),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(8)),
                new Setter(Layoutable.MarginProperty, new Thickness(2, 3))
            }
        });
        Add(new Style(x => x.OfType<ListBoxItem>().Class(":selected").Template().OfType<Avalonia.Controls.Presenters.ContentPresenter>().Name("PART_ContentPresenter"))
        {
            Setters =
            {
                new Setter(Avalonia.Controls.Presenters.ContentPresenter.BackgroundProperty, new DynamicResourceExtension("MySharpSelection")),
                new Setter(Avalonia.Controls.Presenters.ContentPresenter.BorderBrushProperty, new DynamicResourceExtension("MySharpSelectionMark")),
                new Setter(Avalonia.Controls.Presenters.ContentPresenter.BorderThicknessProperty, new Thickness(2, 0, 0, 0))
            }
        });
        Add(new Style(x => x.OfType<ComboBox>())
        {
            Setters =
            {
                new Setter(TemplatedControl.BorderBrushProperty, new DynamicResourceExtension("TextControlBorderBrush")),
                new Setter(TemplatedControl.BackgroundProperty, new DynamicResourceExtension("TextControlBackground")),
                new Setter(TemplatedControl.ForegroundProperty, new DynamicResourceExtension("TextControlForeground")),
                new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(6)),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(10, 6)),
                new Setter(Layoutable.MinHeightProperty, 34d)
            }
        });
        ConfigureControls();
        ConfigureSegments();
    }

    public static FluentTheme CreateFluentBase()
    {
        var fluent = new FluentTheme();
        fluent.Palettes[ThemeVariant.Light] = new ColorPaletteResources { Accent = Color.Parse("#2563EB") };
        fluent.Palettes[ThemeVariant.Dark] = new ColorPaletteResources { Accent = Color.Parse("#2563EB") };
        return fluent;
    }

    private static ResourceDictionary Palette(bool dark)
    {
        var d = new ResourceDictionary();
        string text = dark ? "#F4F4F5" : "#18181B", muted = dark ? "#A1A1AA" : "#71717A";
        string background = dark ? "#18181B" : "#FFFFFF", surface = dark ? "#27272A" : "#F4F4F5";
        string hover = dark ? "#323236" : "#E9E9EC", pressed = dark ? "#3C3C41" : "#DDDDE1";
        string border = dark ? "#3F3F46" : "#D9D9DE", accent = "#2563EB";
        string accentHover = "#1D4ED8", accentPressed = "#1E40AF";
        IBrush Brush(string hex) => new ImmutableSolidColorBrush(Color.Parse(hex));
        void Put(string key, string hex) => d[key] = Brush(hex);
        foreach (var state in new[] { "", "PointerOver", "Pressed" })
        {
            var face = state == "PointerOver" ? hover : state == "Pressed" ? pressed : surface;
            var blue = state == "PointerOver" ? accentHover : state == "Pressed" ? accentPressed : accent;
            foreach (var type in new[] { "Button", "RepeatButton", "ToggleButton" })
            {
                Put(type + "Background" + state, face);
                Put(type + "Foreground" + state, text);
                Put(type + "BorderBrush" + state, border);
            }
            Put("AccentButtonBackground" + state, blue);
            Put("AccentButtonForeground" + state, "#FFFFFF");
            Put("AccentButtonBorderBrush" + state, blue);
            Put("TextControlBackground" + state, background);
            Put("TextControlForeground" + state, text);
            Put("TextControlBorderBrush" + state, border);
            Put("TextControlPlaceholderForeground" + state, muted);
        }
        Put("TextControlBackgroundFocused", background);
        Put("TextControlForegroundFocused", text);
        Put("TextControlPlaceholderForegroundFocused", muted);
        Put("TextControlBorderBrushFocused", accent);
        Put("TextControlSelectionHighlightColor", dark ? "#663B82F6" : "#402563EB");
        Put("MySharpSelection", dark ? "#253552" : "#EAF0FF");
        Put("MySharpSelectionHover", dark ? "#304363" : "#DCE7FF");
        Put("MySharpSelectionPressed", dark ? "#3A5074" : "#CEDCFA");
        Put("MySharpSelectionText", dark ? "#93C5FD" : "#1D4ED8");
        Put("MySharpSelectionMark", dark ? "#60A5FA" : "#2563EB");
        foreach (var level in new[] { "Low", "Medium", "High" })
            Put("SystemControlHighlightListAccent" + level + "Brush", dark ? "#253552" : "#EAF0FF");
        Put("ApplicationPageBackgroundThemeBrush", background);
        Put("SystemControlForegroundBaseHighBrush", text);
        foreach (var key in new[] { "SystemControlBackgroundAccentBrush", "SystemControlForegroundAccentBrush", "SystemControlHighlightAccentBrush", "SystemControlHighlightAltAccentBrush" }) Put(key, accent);
        foreach (var suffix in new[] { "", "Light1", "Light2", "Light3", "Dark1", "Dark2", "Dark3" }) d["SystemAccentColor" + suffix] = Color.Parse(accent);
        foreach (var type in new[] { "Button", "AccentButton", "RepeatButton", "ToggleButton" })
        {
            Put(type + "BackgroundDisabled", surface);
            Put(type + "ForegroundDisabled", muted);
            Put(type + "BorderBrushDisabled", border);
        }
        AddControlPalette(d, dark);
        return d;
    }
}
