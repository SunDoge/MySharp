using System.Runtime.CompilerServices;

namespace MySharp;

public static partial class Ui
{
    [Widget]
    public static Element TextInput(UiContext cx, State<string> value, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        TextInput(cx, value.Value, value.Setter, key: key, site: site, file: file, line: line);

    [Widget]
    public static Element Checkbox(UiContext cx, string label, State<bool> value, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Checkbox(cx, label, value.Value, value.Setter, key: key, site: site, file: file, line: line);

    [Widget]
    public static Element Switch(UiContext cx, string label, State<bool> value, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Switch(cx, label, value.Value, value.Setter, key: key, site: site, file: file, line: line);

    [Widget]
    public static Element Slider(UiContext cx, State<double> value, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Slider(cx, value.Value, value.Setter, key: key, site: site, file: file, line: line);

    [Widget]
    public static Element NumberInput(UiContext cx, State<decimal?> value, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        NumberInput(cx, value.Value, value.Setter, key: key, site: site, file: file, line: line);

    [Widget]
    public static Element DateInput(UiContext cx, State<DateTimeOffset?> value, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        DateInput(cx, value.Value, value.Setter, key: key, site: site, file: file, line: line);

    [Widget]
    public static Element TimeInput(UiContext cx, State<TimeSpan?> value, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        TimeInput(cx, value.Value, value.Setter, key: key, site: site, file: file, line: line);

    [Widget]
    public static Element Select(UiContext cx, System.Collections.IEnumerable items, State<int> value, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Select(cx, items, value.Value, value.Setter, key: key, site: site, file: file, line: line);

    [Widget]
    public static Element List(UiContext cx, System.Collections.IEnumerable items, State<int> value, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        List(cx, items, value.Value, value.Setter, key: key, site: site, file: file, line: line);

}
