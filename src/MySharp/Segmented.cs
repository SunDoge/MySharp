using System.Collections;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;

namespace MySharp;

public static partial class Ui
{
    private static class SegmentResources
    {
        internal static readonly FuncTemplate<Panel?> ItemsPanel = new(() => new StackPanel { Orientation = Orientation.Horizontal });
    }

    /// <summary>A single-choice segmented group with native selection, focus and arrow-key navigation.</summary>
    [Widget]
    public static Element Segmented(UiContext cx, IEnumerable items, int selected, Action<int> changed, string? key = null,
        CallSiteId site = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        List(cx, items, selected, changed, key, site, file, line).Class("segmented")
            .Set(ItemsControl.ItemsPanelProperty, SegmentResources.ItemsPanel);

    [Widget]
    public static Element Segmented(UiContext cx, IEnumerable items, State<int> selected, string? key = null,
        CallSiteId site = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Segmented(cx, items, selected.Value, selected.Setter, key, site, file, line);
}
