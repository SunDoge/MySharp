using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using MySharp;

public sealed partial class Workspace
{
    private static readonly string[] CollectionTabs = ["Preview", "History"];

    [Component]
    private void CollectionControls(UiContext cx)
    {
        var selected = cx.State((object?)null);
        var tab = cx.State(0);
        var notice = cx.State("Choose a menu action");
        var items = cx.Resource(() => new CollectionItems(notice));
        Ui.Grid(cx, "*,*").Set(Grid.ColumnSpacingProperty, 20d).Children(() =>
        {
            Ui.Column(cx).Gap(12).Padding(20).Border(1, Line).Radius(8).Children(() =>
            {
                Ui.Text(cx, "Tree navigation").FontSize(17).Bold();
                Ui.Tree(cx, items.Folders, selected.Value, value => selected.Value = value)
                    .Label("Chapter tree").Height(160);
                Ui.Text(cx, selected.Value is string file ? file : "Expand a chapter and select a page")
                    .FontSize(12).TextColor(Muted).Wrap();
            });
            Ui.Column(cx).Gap(12).Padding(20).Border(1, Line).Radius(8).Children(() =>
            {
                Ui.Text(cx, "Tabs & menus").FontSize(17).Bold();
                Ui.Menu(cx, items.Menus).Label("Example menu");
                var content = cx.Template<string>((body, name) => Ui.Text(body, name == "Preview"
                    ? "The selected page preview appears here."
                    : "No previous exports for this page.").FontSize(13).Wrap().Padding(12));
                Ui.Tabs(cx, CollectionTabs, tab.Value, value => tab.Value = value)
                    .Set(TabControl.ContentTemplateProperty, content).Label("Page details").Height(116);
                Ui.Text(cx, notice.Value).FontSize(12).TextColor(Muted).Wrap();
            });
        });
    }

    private sealed class CollectionItems : IDisposable
    {
        public TreeViewItem[] Folders { get; } =
        [
            new() { Header = "Chapter 01", ItemsSource = new[] { "001.png", "002.png", "003.png" } },
            new() { Header = "Chapter 02", ItemsSource = new[] { "004.png", "005.png" } }
        ];
        public MenuItem[] Menus { get; }
        private readonly (MenuItem Item, EventHandler<RoutedEventArgs> Handler)[] actions;

        public CollectionItems(State<string> notice)
        {
            var save = new MenuItem { Header = "Save draft" };
            var export = new MenuItem { Header = "Export PNG" };
            var original = new MenuItem { Header = "Show original" };
            actions =
            [
                (save, (_, _) => notice.Value = "Draft saved"),
                (export, (_, _) => notice.Value = "Image exported"),
                (original, (_, _) => notice.Value = "Showing the original image")
            ];
            foreach (var (item, handler) in actions) item.Click += handler;
            Menus =
            [
                new() { Header = "File", ItemsSource = new[] { save, export } },
                new() { Header = "View", ItemsSource = new[] { original } }
            ];
        }

        public void Dispose()
        {
            foreach (var menu in Menus) menu.IsSubMenuOpen = false;
            foreach (var (item, handler) in actions) item.Click -= handler;
        }
    }
}
