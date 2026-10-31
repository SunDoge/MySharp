using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;

namespace MySharp;

public sealed partial class MySharpTheme
{
    private void ConfigureControls()
    {
        Resources["CheckBoxBorderThemeThickness"] = new Thickness(1);
        Resources["RadioButtonBorderThemeThickness"] = 1d;
        Resources["TextControlPlaceholderOpacity"] = 1d;
        Resources["ComboBoxBorderThemeThickness"] = new Thickness(1);
        Resources["ComboBoxDropdownBorderThickness"] = new Thickness(1);
        Resources["ComboBoxDropdownBorderPadding"] = new Thickness(4);
        Resources["ComboBoxDropdownContentMargin"] = new Thickness(0);
        Resources["SliderTrackThemeHeight"] = 4d;
        Resources["SliderHorizontalThumbWidth"] = 16d;
        Resources["SliderHorizontalThumbHeight"] = 16d;
        Resources["SliderVerticalThumbWidth"] = 16d;
        Resources["SliderVerticalThumbHeight"] = 16d;
        Resources["FlyoutBorderThemePadding"] = new Thickness(0);
        Resources["FlyoutContentThemePadding"] = new Thickness(16);
        Resources["MenuFlyoutPresenterThemePadding"] = new Thickness(4);
        Resources["MenuFlyoutItemThemePaddingNarrow"] = new Thickness(10, 7);
        Resources["ToolTipBorderThemePadding"] = new Thickness(10, 6);
        Resources["ToolTipContentThemeFontSize"] = 12d;

        Add(new Style(x => x.OfType<Button>().Class("icon-only"))
        {
            Setters =
            {
                new Setter(Layoutable.MinWidthProperty, 34d),
                new Setter(Layoutable.MinHeightProperty, 34d),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(8))
            }
        });

        Add(new Style(x => x.OfType<Window>())
        {
            Setters =
            {
                new Setter(TemplatedControl.ForegroundProperty, new DynamicResourceExtension("MySharpText")),
                new Setter(TemplatedControl.BackgroundProperty, new DynamicResourceExtension("MySharpBackground"))
            }
        });
        Add(new Style(x => x.OfType<PopupRoot>())
        {
            Setters = { new Setter(TopLevel.TransparencyBackgroundFallbackProperty, new DynamicResourceExtension("MySharpBackground")) }
        });
        Add(new Style(x => x.OfType<CheckBox>())
        {
            Setters = { new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(4)) }
        });
        Add(new Style(x => x.OfType<Button>().Class("navigation"))
        {
            Setters =
            {
                new Setter(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Left),
                new Setter(Layoutable.HorizontalAlignmentProperty, HorizontalAlignment.Stretch),
                new Setter(Layoutable.VerticalAlignmentProperty, VerticalAlignment.Stretch)
            }
        });
        Add(new Style(x => x.OfType<Button>().Class("navigation").Class(":pressed"))
        {
            Setters = { new Setter(Visual.RenderTransformProperty, null) }
        });
        Add(new Style(x => x.OfType<ToggleButton>())
        {
            Setters = { new Setter(Layoutable.MinHeightProperty, 34d) }
        });
        Add(new Style(x => x.OfType<ToggleSwitch>())
        {
            Setters =
            {
                new Setter(ToggleSwitch.OnContentProperty, null),
                new Setter(ToggleSwitch.OffContentProperty, null),
                new Setter(TemplatedControl.TemplateProperty, InlineSwitchTemplate())
            }
        });
        foreach (var part in new[] { (Name: "OuterBorder", State: "Off"), (Name: "SwitchKnobBounds", State: "On") })
        {
            Add(new Style(x => x.OfType<ToggleSwitch>().Template().OfType<Border>().Name(part.Name))
            {
                Setters =
                {
                    new Setter(Border.BackgroundProperty, new DynamicResourceExtension("ToggleSwitchFill" + part.State)),
                    new Setter(Border.BorderBrushProperty, new DynamicResourceExtension("ToggleSwitchStroke" + part.State))
                }
            });
        }
        foreach (var state in new[] { "Off", "On" })
        {
            Add(new Style(x => x.OfType<ToggleSwitch>().Template().OfType<Ellipse>().Name("SwitchKnob" + state))
            {
                Setters = { new Setter(Shape.FillProperty, new DynamicResourceExtension("ToggleSwitchKnobFill" + state)) }
            });
        }
        Add(new Style(x => x.OfType<ComboBoxItem>())
        {
            Setters =
            {
                new Setter(TemplatedControl.PaddingProperty, new Thickness(10, 7)),
                new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(4)),
                new Setter(Layoutable.MinHeightProperty, 32d)
            }
        });
        Add(new Style(x => x.OfType<ProgressBar>())
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, new DynamicResourceExtension("MySharpTrack")),
                new Setter(TemplatedControl.ForegroundProperty, new DynamicResourceExtension("MySharpAccent")),
                new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(3)),
                new Setter(Layoutable.MinHeightProperty, 4d)
            }
        });
    }

    // Keep the native switch parts and pseudo states, including knob dragging,
    // while placing its label inline instead of Fluent's two-row header layout.
    private static FuncControlTemplate<ToggleSwitch> InlineSwitchTemplate() => new((control, scope) =>
    {
        T Named<T>(string name, T part) where T : Control { part.Name = name; scope.Register(name, part); return part; }
        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("40,Auto,Auto"), Background = Brushes.Transparent };
        var track = new Grid { Width = 40, Height = 22, VerticalAlignment = VerticalAlignment.Center };
        track.SetValue(TemplatedControl.IsTemplateFocusTargetProperty, true);
        var off = Named("OuterBorder", new Border { CornerRadius = new CornerRadius(11), BorderThickness = new Thickness(1) });
        var on = Named("SwitchKnobBounds", new Border { CornerRadius = new CornerRadius(11), BorderThickness = new Thickness(1) });
        var canvas = Named("PART_SwitchKnob", new Canvas { Width = 18, Height = 22, HorizontalAlignment = HorizontalAlignment.Left });
        var knobs = Named("PART_MovingKnobs", new Grid { Width = 22, Height = 22, Background = Brushes.Transparent });
        var offKnob = Named("SwitchKnobOff", new Ellipse { Width = 14, Height = 14 });
        var onKnob = Named("SwitchKnobOn", new Ellipse { Width = 14, Height = 14 });
        knobs.Children.Add(offKnob); knobs.Children.Add(onKnob); canvas.Children.Add(knobs);
        track.Children.Add(off); track.Children.Add(on); track.Children.Add(canvas);
        root.Children.Add(track);
        var label = Named("PART_ContentPresenter", new ContentPresenter
        {
            Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
            RecognizesAccessKey = true,
            [!ContentPresenter.ContentProperty] = new TemplateBinding(ContentControl.ContentProperty),
            [!ContentPresenter.ContentTemplateProperty] = new TemplateBinding(ContentControl.ContentTemplateProperty)
        });
        Grid.SetColumn(label, 1); root.Children.Add(label);
        foreach (var enabled in new[] { false, true })
        {
            var status = Named(enabled ? "PART_OnContentPresenter" : "PART_OffContentPresenter", new ContentPresenter
            {
                Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                [!ContentPresenter.ContentProperty] = new TemplateBinding(enabled ? ToggleSwitch.OnContentProperty : ToggleSwitch.OffContentProperty),
                [!ContentPresenter.ContentTemplateProperty] = new TemplateBinding(enabled ? ToggleSwitch.OnContentTemplateProperty : ToggleSwitch.OffContentTemplateProperty)
            });
            Grid.SetColumn(status, 2); root.Children.Add(status);
        }
        return root;
    });

    // Override every interactive state used by the native templates. Changing only
    // SystemAccentColor leaves many Fluent controls with unrelated surfaces/strokes.
    private static void AddControlPalette(ResourceDictionary d, bool dark)
    {
        string text = dark ? "#F4F4F5" : "#18181B", muted = dark ? "#A1A1AA" : "#71717A";
        string background = dark ? "#18181B" : "#FFFFFF", surface = dark ? "#27272A" : "#F4F4F5";
        string hover = dark ? "#323236" : "#E9E9EC", pressed = dark ? "#3C3C41" : "#DDDDE1";
        string border = dark ? "#3F3F46" : "#D9D9DE", strongBorder = dark ? "#71717A" : "#A1A1AA";
        string accent = dark ? "#3B82F6" : "#2563EB";
        string selection = dark ? "#253552" : "#EAF0FF", selectionText = dark ? "#93C5FD" : "#1D4ED8";
        void Put(string key, string value) => d[key] = new ImmutableSolidColorBrush(Color.Parse(value));
        Put("MySharpText", text);
        Put("MySharpTextMuted", muted);
        Put("MySharpBackground", background);
        Put("MySharpSurface", surface);
        Put("MySharpBorder", border);
        Put("MySharpAccent", accent);
        Put("MySharpTrack", border);
        Put("ToolTipBackground", dark ? "#F4F4F5" : "#27272A");
        Put("ToolTipForeground", dark ? "#18181B" : "#FFFFFF");
        Put("ToolTipBorderBrush", "#00000000");
        Put("FlyoutPresenterBackground", background);
        Put("FlyoutBorderThemeBrush", border);
        Put("MenuFlyoutPresenterBackground", background);
        Put("MenuFlyoutPresenterBorderBrush", border);
        Put("ComboBoxDropDownBackground", background);
        Put("ComboBoxDropDownBorderBrush", border);
        Put("ComboBoxBackgroundUnfocused", background);
        Put("ComboBoxBackgroundBorderBrushUnfocused", border);
        Put("ComboBoxBackgroundBorderBrushFocused", accent);
        Put("ComboBoxForegroundFocused", text);
        Put("ComboBoxForegroundFocusedPressed", text);
        Put("ComboBoxDropDownGlyphForegroundFocused", muted);
        Put("ComboBoxDropDownGlyphForegroundFocusedPressed", muted);
        Put("ComboBoxPlaceHolderForeground", muted);
        Put("ComboBoxPlaceHolderForegroundFocusedPressed", muted);
        Put("SystemControlHighlightListLowBrush", hover);
        Put("SystemControlHighlightListMediumBrush", pressed);
        Put("SystemControlHighlightAltBaseHighBrush", text);
        Put("SystemControlErrorTextForegroundBrush", dark ? "#F87171" : "#DC2626");

        Put("TextControlBackgroundDisabled", surface);
        Put("TextControlForegroundDisabled", muted);
        Put("TextControlBorderBrushDisabled", border);
        foreach (var state in new[] { "", "PointerOver", "Pressed", "Disabled" })
        {
            bool disabled = state == "Disabled";
            string face = state == "PointerOver" ? hover : state == "Pressed" ? pressed : surface;
            string blue = disabled ? border : state == "PointerOver" ? (dark ? "#60A5FA" : "#1D4ED8")
                : state == "Pressed" ? (dark ? "#2563EB" : "#1E40AF") : accent;
            string ink = disabled ? muted : text;
            string stroke = state is "PointerOver" or "Pressed" ? strongBorder : border;
            Put("ComboBoxBackground" + state, disabled ? surface : state == "" ? background : face);
            Put("ComboBoxBorderBrush" + state, stroke);
            Put("ComboBoxForeground" + state, ink);
            Put("ComboBoxDropDownGlyphForeground" + state, muted);
            if (state is "PointerOver" or "Pressed") Put("TextControlBorderBrush" + state, stroke);

            foreach (var checkState in new[] { "Unchecked", "Checked", "Indeterminate" })
            {
                bool check = checkState != "Unchecked";
                Put("CheckBoxForeground" + checkState + state, ink);
                Put("CheckBoxBackground" + checkState + state, "#00000000");
                Put("CheckBoxBorderBrush" + checkState + state, "#00000000");
                Put("CheckBoxCheckBackgroundFill" + checkState + state, check ? blue : face);
                Put("CheckBoxCheckBackgroundStroke" + checkState + state, check ? blue : stroke);
                Put("CheckBoxCheckGlyphForeground" + checkState + state, disabled ? muted : "#FFFFFF");
            }
            Put("RadioButtonForeground" + state, ink);
            Put("RadioButtonOuterEllipseFill" + state, face);
            Put("RadioButtonOuterEllipseStroke" + state, stroke);
            Put("RadioButtonOuterEllipseCheckedFill" + state, blue);
            Put("RadioButtonOuterEllipseCheckedStroke" + state, blue);
            Put("RadioButtonCheckGlyphFill" + state, disabled ? muted : "#FFFFFF");
            Put("RadioButtonCheckGlyphStroke" + state, "#00000000");
            Put("ToggleSwitchContentForeground", text);
            Put("ToggleSwitchHeaderForegroundDisabled", muted);
            Put("ToggleSwitchFillOff" + state, face);
            Put("ToggleSwitchStrokeOff" + state, stroke);
            Put("ToggleSwitchFillOn" + state, blue);
            Put("ToggleSwitchStrokeOn" + state, blue);
            Put("ToggleSwitchKnobFillOff" + state, muted);
            Put("ToggleSwitchKnobFillOn" + state, disabled ? muted : "#FFFFFF");
            Put("SliderTrackFill" + state, border);
            Put("SliderTrackValueFill" + state, blue);
            Put("SliderThumbBackground" + state, blue);
            foreach (var check in new[] { "Checked", "Indeterminate" })
            {
                Put("ToggleButtonBackground" + check + state, disabled ? surface : selection);
                Put("ToggleButtonForeground" + check + state, disabled ? muted : selectionText);
                Put("ToggleButtonBorderBrush" + check + state, disabled ? border : accent);
            }
            foreach (var selected in new[] { "", "Selected" })
            {
                Put("ComboBoxItemBackground" + selected + state, selected != "" && !disabled ? selection : state == "" || disabled ? "#00000000" : face);
                Put("ComboBoxItemForeground" + selected + state, selected != "" && !disabled ? selectionText : ink);
                Put("ComboBoxItemBorderBrush" + selected + state, "#00000000");
            }
            Put("MenuFlyoutItemBackground" + state, state == "" || disabled ? "#00000000" : face);
            Put("MenuFlyoutItemForeground" + state, ink);
            Put("MenuFlyoutItemKeyboardAcceleratorTextForeground" + state, muted);
            Put("MenuFlyoutSubItemChevron" + state, muted);
        }
    }
}
