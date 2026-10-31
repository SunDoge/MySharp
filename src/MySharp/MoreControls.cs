using System.Collections;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace MySharp;

public static partial class Ui
{
    [Widget]
    public static Element Toggle(UiContext cx, string label, bool value, Action<bool> changed, string? key = null,
        CallSiteId site = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Control<ToggleButton>(cx, key, site, file, line).Set<object?>(ContentControl.ContentProperty, label)
            .Set<bool?>(ToggleButton.IsCheckedProperty, value).OnChanged(ToggleButton.IsCheckedProperty, v => changed(v == true));

    [Widget]
    public static Element Radio(UiContext cx, string label, string group, bool value, Action<bool> changed, string? key = null,
        CallSiteId site = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Control<RadioButton>(cx, key, site, file, line).Set<object?>(ContentControl.ContentProperty, label)
            .Set(RadioButton.GroupNameProperty, group).Set<bool?>(ToggleButton.IsCheckedProperty, value)
            .OnChanged(ToggleButton.IsCheckedProperty, v => changed(v == true));

    [Widget]
    public static Element Image(UiContext cx, IImage? source, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Control<Image>(cx, key, site, file, line).Set(Avalonia.Controls.Image.SourceProperty, source);

    [Widget]
    public static Element Separator(UiContext cx, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) => Control<Separator>(cx, key, site, file, line);

    [Widget]
    public static Element Tabs(UiContext cx, IEnumerable items, int selected, Action<int> changed, string? key = null,
        CallSiteId site = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Control<TabControl>(cx, key, site, file, line).Set<IEnumerable?>(ItemsControl.ItemsSourceProperty, items)
            .Set(SelectingItemsControl.SelectedIndexProperty, selected).OnChanged(SelectingItemsControl.SelectedIndexProperty, changed);

    [Widget]
    public static Element Tree(UiContext cx, IEnumerable items, object? selected, Action<object?> changed, string? key = null,
        CallSiteId site = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Control<TreeView>(cx, key, site, file, line).Set<IEnumerable?>(ItemsControl.ItemsSourceProperty, items)
            .Set(TreeView.SelectedItemProperty, selected).OnChanged(TreeView.SelectedItemProperty, changed);

    [Widget]
    public static Element Menu(UiContext cx, IEnumerable items, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Control<Menu>(cx, key, site, file, line).Set<IEnumerable?>(ItemsControl.ItemsSourceProperty, items);

    [Widget]
    public static Element Expander(UiContext cx, string header, bool expanded, Action<bool> changed, string? key = null,
        CallSiteId site = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Control<Expander>(cx, key, site, file, line).Set<object?>(HeaderedContentControl.HeaderProperty, header)
            .Set(Avalonia.Controls.Expander.IsExpandedProperty, expanded).OnChanged(Avalonia.Controls.Expander.IsExpandedProperty, changed);
}
