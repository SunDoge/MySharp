using System.Globalization;
using System.Resources;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using MySharp;

internal static class I18nTests
{
    internal static int Run()
    {
        var count = 0;
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        void Test(string name, Action run) { run(); count++; Console.WriteLine("PASS i18n " + name); }
        ResourceManager Resources() => new("MySharp.examples.Workspace.Localization.Strings", typeof(Workspace).Assembly);
        var english = CultureInfo.GetCultureInfo("en-US"); var chinese = CultureInfo.GetCultureInfo("zh-CN");
        Test("resx satellites default fallback and missing diagnostics", () =>
        {
            using var t = new I18n(Resources(), chinese);
            Check(t["settings.title"] == "设置", "Chinese satellite resource");
            Check(t["fallback.example"] == "Default-language fallback", "Neutral resource fallback");
            var misses = 0; t.MissingTranslation += (_, culture) => { misses++; Check(culture.Name == "zh-CN", "Diagnostic culture"); };
            Check(t["missing"] == "missing" && t["missing"] == "missing" && misses == 1, "Readable key and deduplicated diagnostic");
        });
        Test("formatting culture snapshots and plural rules", () =>
        {
            var mutable = (CultureInfo)english.Clone();
            using var t = new I18n(Resources(), mutable);
            mutable.NumberFormat.NumberDecimalSeparator = "!";
            Check(!t.Format("format.example", 12345.67, new DateTime(2026, 11, 2)).Contains('!'), "Caller mutation cannot silently change culture");
            Check(t.Plural("process.count", 1) == "1 process" && t.Plural("process.count", 2) == "2 processes" && t.Plural("process.count", -1) == "-1 process", "English plural");
            t.Culture = chinese; Check(t.Plural("process.count", 1) == "1 个进程", "Chinese other category");
            t.Culture = CultureInfo.GetCultureInfo("ar"); Check(t.FlowDirection == FlowDirection.RightToLeft, "RTL metadata");
            try { t.Plural("process.count", 3); throw new Exception("Unsupported rule silently accepted"); } catch (NotSupportedException) { }
            using var custom = new I18n((key, _) => key == "items.few" ? "{0} items" : null, CultureInfo.GetCultureInfo("ar"), (_, _) => "few");
            Check(custom.Plural("items", 3).Contains("items"), "Injected rule and provider");
        });
        Test("language changes retain editor focus caret and shared views", () =>
        {
            using var t = new I18n(Resources(), english);
            var processCulture = CultureInfo.CurrentCulture; var uiCulture = CultureInfo.CurrentUICulture;
            var builds = 0;
            using var first = new UiView(cx =>
            {
                builds++; var text = cx.UseI18n(t); var draft = cx.State("draft");
                Ui.Column(cx).Children(() => { Ui.Text(cx, text["settings.title"]); Ui.TextInput(cx, draft).Label("draft"); });
            });
            using var second = new UiView(cx => Ui.Text(cx, cx.UseI18n(t)["settings.title"]));
            var window = new Window { Content = first, Width = 500, Height = 250 }; window.Show(); Dispatcher.UIThread.RunJobs();
            try
            {
                var editor = first.GetLogicalDescendants().OfType<TextBox>().Single(); editor.Text = "my unsaved draft"; editor.Focus(); editor.CaretIndex = 4;
                t.Culture = chinese; Dispatcher.UIThread.RunJobs();
                Check(first.GetLogicalDescendants().OfType<TextBlock>().Any(b => b.Text == "设置") && second.GetLogicalDescendants().OfType<TextBlock>().Single().Text == "设置", "Both views refresh");
                Check(ReferenceEquals(editor, first.GetLogicalDescendants().OfType<TextBox>().Single()) && editor.Text == "my unsaved draft" && editor.CaretIndex == 4 && editor.IsFocused, "Retained editor, focus, caret and draft");
                var previous = builds; t.Culture = chinese; Dispatcher.UIThread.RunJobs(); Check(builds == previous, "Same cached culture does not rebuild");
                Check(ReferenceEquals(processCulture, CultureInfo.CurrentCulture) && ReferenceEquals(uiCulture, CultureInfo.CurrentUICulture), "No global culture mutation");
                first.Dispose(); second.Dispose(); t.Culture = english; Dispatcher.UIThread.RunJobs(); Check(builds == previous, "Disposed views unsubscribe");
            }
            finally { window.Close(); }
        });
        Test("Gallery settings switches language while retaining draft", () =>
        {
            using var workspace = new Workspace("settings"); using var view = new UiView(workspace.Build);
            var editor = view.GetLogicalDescendants().OfType<TextBox>().Single(b => AutomationProperties.GetName(b) == "Display name");
            editor.Text = "保留草稿";
            var languages = view.GetLogicalDescendants().OfType<ListBox>().Single(); languages.SelectedIndex = 1; Dispatcher.UIThread.RunJobs();
            Check(view.GetLogicalDescendants().OfType<TextBlock>().Any(b => b.Text == "设置"), "Chinese settings");
            Check(ReferenceEquals(editor, view.GetLogicalDescendants().OfType<TextBox>().Single(b => AutomationProperties.GetName(b) == "显示名称")) && editor.Text == "保留草稿", "Draft retained");
        });
        return count;
    }
}
