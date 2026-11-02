using MySharp;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
#if DEBUG
        MySharpHotReload.Enable();
#endif
        var page = args.FirstOrDefault(a => a.StartsWith("--page="))?.Split('=')[1] ?? "overview";
        var customChrome = args.Contains("--custom-chrome");
        var dark = args.Contains("--dark");
        var gameSavePath = args.Contains("--no-game-save") ? null : args.FirstOrDefault(a => a.StartsWith("--game-save="))?[12..]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MySharp", "Gallery", "2048.json");
        using var workspace = new Workspace(page, customChrome, dark, gameSavePath, args.Contains("--reduced-motion"));
        MySharpApp.Run(workspace.Build, new WindowOptions
        {
            Chrome = customChrome ? WindowChrome.Custom : WindowChrome.System,
            Title = "MySharp · Gallery", Width = 1120, Height = 820, MinWidth = 960, MinHeight = 680,
            Theme = dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light
        }, args);
    }
}

public sealed partial class Workspace(string initialPage = "overview", bool customChrome = false, bool dark = false, string? gameSavePath = null, bool reducedMotion = false) : IDisposable
{
    private readonly State<int> sharedClicks = State.Create(0);
    public void Dispose() { sharedClicks.Dispose(); translations.Dispose(); }
    private readonly I18n translations = new(new System.Resources.ResourceManager("MySharp.examples.Workspace.Localization.Strings", typeof(Workspace).Assembly), System.Globalization.CultureInfo.GetCultureInfo("en-US"));

    private string Ink => dark ? "#F4F4F5" : "#18181B";
    private string Muted => dark ? "#A1A1AA" : "#71717A";
    private string Blue => dark ? "#60A5FA" : "#2563EB";
    private string Line => dark ? "#3F3F46" : "#E4E4E7";
    private string PageBackground => dark ? "#18181B" : "#FFFFFF";
    private string Surface => dark ? "#222225" : "#F7F7F8";
    private string Selection => dark ? "#253552" : "#EAF0FF";
    private string Error => dark ? "#F87171" : "#A63838";
    private string page = initialPage is "tasks" or "settings" or "lab" or "controls" or "2048" or "pulse" ? initialPage : "overview";
    private string search = "", draft = "", filter = "All";
    private string displayName = "Alex Chen", workspaceName = "Personal studio", savedMessage = "";
    private bool showActivity = true, compact = false;
    private string draftName = "Alex Chen", draftWorkspace = "Personal studio";
    private bool draftShowActivity = true, draftCompact = false;
    private int nextId = 5;
    private readonly List<WorkItem> tasks =
    [
        new(1, "Build the component gallery", "MySharp", "Today", false),
        new(2, "Review keyboard navigation", "MySharp", "Today", false),
        new(3, "Document the build workflow", "MyGo", "Tomorrow", false),
        new(4, "Explore layout references", "Design notes", "Completed", true)
    ];

    [Component]
    public void Build(UiContext cx)
    {
        cx.UseI18n(translations);
        var leavePulse = cx.State(false);
        if (leavePulse.Value) { page = "overview"; leavePulse.Value = false; }
        if (page == "pulse") { PulsePage(cx, () => leavePulse.Value = true); return; }
        if (!customChrome) { Shell(cx); return; }
        Ui.Grid(cx, "*").Rows("44,*").Fill().Children(() =>
        {
            Ui.Grid(cx, "*,Auto").Cell(0, 0).TitleBar().Background(Surface).Padding(12, 4).Children(() =>
            {
                Ui.Text(cx, "MySharp · Gallery").FontSize(13).Bold().Label("Window title");
                Ui.WindowControls(cx);
            });
            Shell(cx).Cell(1, 0);
        });
    }

    [Component]
    private Element Shell(UiContext cx)
    {
        return Ui.Grid(cx, "216,*").Fill().Background(PageBackground).Children(() =>
        {
            Ui.Grid(cx, "*").Rows("*,Auto").Fill().Background(Surface).Children(() =>
            {
                Ui.Scroll(cx).Cell(0, 0).Fill().Children(() =>
                {
                    Ui.Column(cx).Gap(6).Padding(20, 28).Children(() =>
                    {
                        Ui.Text(cx, "MySharp").FontSize(23).Bold().TextColor(Ink);
                        Ui.Text(cx, workspaceName).Wrap().FontSize(12).TextColor(Muted);
                        Ui.Space(cx, 28);
                        cx.Scope(() => Navigation(cx, "overview", translations["nav.overview"], Icons.Home));
                        cx.Scope(() => Navigation(cx, "tasks", translations["nav.tasks"], Icons.Tasks));
                        cx.Scope(() => Navigation(cx, "settings", translations["nav.settings"], Icons.Settings));
                        cx.Scope(() => Navigation(cx, "lab", translations["nav.lab"], Icons.Activity));
                        cx.Scope(() => Navigation(cx, "controls", translations["nav.controls"], Icons.Components));
                        cx.Scope(() => Navigation(cx, "2048", "2048", Lucide.Avalonia.LucideIconKind.Gamepad2));
                        cx.Scope(() => Navigation(cx, "pulse", "Pulse", Icons.Activity));
                        Ui.Space(cx, 32);
                        Ui.Text(cx, "Workspace").FontSize(12).TextColor(Muted);
                        Ui.Text(cx, "MySharp").FontSize(13).TextColor(Ink).Padding(10);
                        Ui.Text(cx, "MyGo").FontSize(13).TextColor(Ink).Padding(10);
                        Ui.Text(cx, "Design notes").FontSize(13).TextColor(Ink).Padding(10);
                    });
                });
                Ui.Column(cx).Cell(1, 0).Gap(5).Padding(30, 20).Children(() =>
                {
                    Ui.Text(cx, displayName).Wrap().FontSize(13).Bold().TextColor(Ink);
                    Ui.Text(cx, "Local workspace · Demo").Wrap().FontSize(12).TextColor(Muted);
                });
            });
            if (page == "2048") { cx.Scope(() => GamePage(cx)); return; }
            Ui.Scroll(cx).Fill().Children(() =>
            {
                Ui.Column(cx).Gap(22).Padding(36, 28).Children(() =>
                {
                    Ui.Text(cx, $"Gallery  /  {page}").FontSize(12).TextColor(Muted);
                    switch (page)
                    {
                        case "tasks": cx.Scope(() => TasksPage(cx)); break;
                        case "settings": cx.Scope(() => SettingsPage(cx)); break;
                        case "controls": cx.Scope(() => ControlsPage(cx)); break;
                        case "lab": cx.Scope(() => LabPage(cx)); break;
                        default: cx.Scope(() => OverviewPage(cx)); break;
                    }
                });
            });
        });
    }

    [Component]
    private void Navigation(UiContext cx, string target, string label, Lucide.Avalonia.LucideIconKind icon)
    {
        Ui.Button(cx, label).Icon(icon).Class("quiet").Class("navigation").Class("selected", page == target)
            .FontSize(14).Height(42).OnClick(() => page = target);
    }

    [Component]
    private void Heading(UiContext cx, string title, string subtitle)
    {
        Ui.Column(cx).Gap(8).Children(() =>
        {
            Ui.Text(cx, title).FontSize(24).Bold().TextColor(Ink);
            Ui.Text(cx, subtitle).FontSize(13).TextColor(Muted);
        });
    }

    private sealed class WorkItem(int id, string title, string project, string due, bool done)
    {
        public int Id = id;
        public string Title = title, Project = project, Due = due;
        public bool Done = done;
    }
}
