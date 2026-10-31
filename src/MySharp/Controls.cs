using System.Collections;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace MySharp;

public static partial class Ui
{
    [Widget]
    public static Element Checkbox(UiContext cx, string label, bool value, Action<bool> changed, string? key = null,
        CallSiteId site = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Control<CheckBox>(cx, key, site, file, line).Set<object?>(ContentControl.ContentProperty, label)
            .Set<bool?>(ToggleButton.IsCheckedProperty, value).OnChanged(ToggleButton.IsCheckedProperty, v => changed(v == true));

    [Widget]
    public static Element Switch(UiContext cx, string label, bool value, Action<bool> changed, string? key = null,
        CallSiteId site = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Control<ToggleSwitch>(cx, key, site, file, line).Set<object?>(ContentControl.ContentProperty, label)
            .Set<bool?>(ToggleButton.IsCheckedProperty, value).OnChanged(ToggleButton.IsCheckedProperty, v => changed(v == true));

    [Widget]
    public static Element Slider(UiContext cx, double value, Action<double> changed, double minimum = 0, double maximum = 100,
        string? key = null, CallSiteId site = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Control<Slider>(cx, key, site, file, line).Set(RangeBase.MinimumProperty, minimum)
            .Set(RangeBase.MaximumProperty, maximum).Set(RangeBase.ValueProperty, value).OnChanged(RangeBase.ValueProperty, changed);

    [Widget]
    public static Element NumberInput(UiContext cx, decimal? value, Action<decimal?> changed, decimal minimum = decimal.MinValue,
        decimal maximum = decimal.MaxValue, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Control<NumericUpDown>(cx, key, site, file, line).Set(NumericUpDown.MinimumProperty, minimum)
            .Set(NumericUpDown.MaximumProperty, maximum).Set(NumericUpDown.ValueProperty, value)
            .OnChanged(NumericUpDown.ValueProperty, changed);

    [Widget]
    public static Element Select(UiContext cx, IEnumerable items, int selected, Action<int> changed, string? key = null,
        CallSiteId site = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Control<ComboBox>(cx, key, site, file, line).Set<IEnumerable?>(ItemsControl.ItemsSourceProperty, items)
            .Set(SelectingItemsControl.SelectedIndexProperty, selected).OnChanged(SelectingItemsControl.SelectedIndexProperty, changed);

    [Widget]
    public static Element DateInput(UiContext cx, DateTimeOffset? value, Action<DateTimeOffset?> changed, string? key = null,
        CallSiteId site = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Control<DatePicker>(cx, key, site, file, line).Set(DatePicker.SelectedDateProperty, value)
            .OnChanged(DatePicker.SelectedDateProperty, changed);

    [Widget]
    public static Element TimeInput(UiContext cx, TimeSpan? value, Action<TimeSpan?> changed, string? key = null,
        CallSiteId site = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Control<TimePicker>(cx, key, site, file, line).Set(TimePicker.SelectedTimeProperty, value)
            .OnChanged(TimePicker.SelectedTimeProperty, changed);

    [Widget]
    public static Element Progress(UiContext cx, double value = 0, bool indeterminate = false, string? key = null,
        CallSiteId site = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Control<ProgressBar>(cx, key, site, file, line).Set(RangeBase.ValueProperty, value)
            .Set(ProgressBar.IsIndeterminateProperty, indeterminate);

    [Widget]
    public static Element List(UiContext cx, IEnumerable items, int selected, Action<int> changed, string? key = null,
        CallSiteId site = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Control<ListBox>(cx, key, site, file, line).Set<IEnumerable?>(ItemsControl.ItemsSourceProperty, items)
            .Set(SelectingItemsControl.SelectedIndexProperty, selected).OnChanged(SelectingItemsControl.SelectedIndexProperty, changed);

    [Widget]
    public static Element Wrap(UiContext cx, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) => Control<WrapPanel>(cx, key, site, file, line);

    [Widget]
    public static Element Dock(UiContext cx, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) => Control<DockPanel>(cx, key, site, file, line);
}
