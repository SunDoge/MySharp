using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.Threading;
using MySharp;

internal static class CollectionTests
{
    internal static int Run()
    {
        var app = Application.Current!;
        var fluent = MySharpTheme.CreateFluentBase();
        var theme = new MySharpTheme();
        app.Styles.Add(fluent); app.Styles.Add(theme);
        foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            using var workspace = new Workspace("controls", dark: variant == ThemeVariant.Dark);
            using var view = new UiView(workspace.Build);
            var window = new Window { Width = 1120, Height = 820, RequestedThemeVariant = variant, Content = view };
            void Pump() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
            void Assert(bool value, string message) { if (!value) throw new Exception(message); }
            void Key(PhysicalKey key)
            {
                window.KeyPressQwerty(key, RawInputModifiers.None);
                window.KeyReleaseQwerty(key, RawInputModifiers.None); Pump();
            }
            try
            {
                window.Show(); Pump();
                var tree = view.GetLogicalDescendants().OfType<TreeView>().Single();
                tree.BringIntoView(); Pump();
                var chapter = (TreeViewItem)tree.Items[0]!;
                chapter.IsSelected = true; chapter.Focus(); Key(PhysicalKey.ArrowRight);
                Assert(chapter.IsExpanded, "Right arrow expands the native chapter tree.");
                Key(PhysicalKey.ArrowDown);
                Assert(Equals(tree.SelectedItem, "001.png"), "Down arrow selects the first child page.");
                view.Rebuild(); Pump();
                Assert(chapter.IsExpanded && Equals(tree.SelectedItem, "001.png"), "Rebuilding retains expansion and native tree selection.");
                var tabs = view.GetLogicalDescendants().OfType<TabControl>().Single();
                var firstTab = (TabItem)tabs.ContainerFromIndex(0)!;
                Assert(firstTab.FontSize == 14 && firstTab.Bounds.Height <= 36, "Tab headers share compact UI typography and metrics.");
                firstTab.Focus(); Key(PhysicalKey.ArrowRight);
                Assert(tabs.SelectedIndex == 1 && view.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "No previous exports for this page."), "Native tab keyboard navigation changes the declared content.");
                var menu = view.GetLogicalDescendants().OfType<Menu>().Single();
                menu.BringIntoView(); Pump();
                var file = (MenuItem)menu.Items[0]!;
                menu.Open(); file.Focus(); Key(PhysicalKey.ArrowDown);
                Assert(file.IsSubMenuOpen, "Menu keyboard navigation opens the submenu.");
                Key(PhysicalKey.Enter);
                Assert(view.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Draft saved"), "Enter dispatches the current menu action through local state.");
                var retained = menu;
                view.Rebuild(); Pump();
                Assert(ReferenceEquals(retained, view.GetLogicalDescendants().OfType<Menu>().Single()), "Menu declarations retain their native control and actions.");
            }
            finally { window.Close(); }
        }
        app.Styles.Remove(theme); app.Styles.Remove(fluent);
        Console.WriteLine("PASS gallery tree tabs and menu native keyboard state and retained controls in light and dark");
        return 1;
    }
}
