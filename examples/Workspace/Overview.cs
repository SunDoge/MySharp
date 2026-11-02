using MySharp;

public sealed partial class Workspace
{
    [Component]
    private void OverviewPage(UiContext cx)
    {
        cx.Scope(() => Heading(cx, "Overview", "Your projects, priorities and recent work in one place."));
        Ui.Grid(cx, "*,*,*").Children(() =>
        {
            cx.Scope(() => Metric(cx, "Projects", "03", "Two in active development"));
            cx.Scope(() => Metric(cx, "Open tasks", tasks.Count(t => !t.Done).ToString("D2"), "Keep the next step small"));
            cx.Scope(() => Metric(cx, "Completed", tasks.Count(t => t.Done).ToString("D2"), "Progress, one task at a time"));
        });
        Ui.Grid(cx, "*,150").Children(() =>
        {
            Ui.Text(cx, "Projects").FontSize(19).Bold().TextColor(Ink);
            Ui.Button(cx, "View tasks").Icon(Icons.ArrowRight).Flat().TextColor(Blue).OnClick(() => page = "tasks");
        });
        Ui.TextInput(cx, search, value => search = value).Placeholder("Search projects…").Label("Search projects");
        Ui.Grid(cx, "*,140,110").Padding(16, 0).Children(() =>
        {
            Ui.Text(cx, "Name").FontSize(12).TextColor(Muted);
            Ui.Text(cx, "Language").FontSize(12).TextColor(Muted);
            Ui.Text(cx, "Status").FontSize(12).TextColor(Muted);
        });
        var projects = new[]
        {
            (Id: "mysharp", Name: "MySharp", Detail: "A small, expressive desktop UI toolkit", Language: "C# / Avalonia", Status: "Prototype"),
            (Id: "mygo", Name: "MyGo", Detail: "Native interfaces and desktop tools", Language: "Go", Status: "Active"),
            (Id: "notes", Name: "Design notes", Detail: "References, experiments and little ideas", Language: "Markdown", Status: "Draft")
        };
        cx.Scope(() =>
        {
            foreach (var project in projects.Where(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase)))
                cx.Scope(project.Id, () => ProjectCard(cx, project.Name, project.Detail, project.Language, project.Status));
        });
        if (!projects.Any(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase)))
            Ui.Text(cx, "No matching projects. Try another name.").TextColor(Muted).Padding(16);
        if (showActivity)
        {
            Ui.Text(cx, "Recent activity").FontSize(19).Bold().TextColor(Ink);
            Ui.Column(cx).Gap(12).Padding(18).Radius(8).Background(Surface).Children(() =>
            {
                Ui.Text(cx, "MySharp   /   Component identities are ready").FontSize(13).TextColor(Ink);
                Ui.Text(cx, "Today · Added stable call sites and instance scopes").FontSize(12).TextColor(Muted);
            });
        }
    }

    [Component]
    private void Metric(UiContext cx, string label, string value, string detail)
    {
        Ui.Column(cx).Gap(10).Padding(18).Margin(4).Border(1, Line).Radius(8).Children(() =>
        {
            Ui.Text(cx, label).FontSize(12).TextColor(Muted);
            Ui.Text(cx, value).FontSize(32).Bold().TextColor(Ink);
            Ui.Text(cx, detail).FontSize(12).TextColor(Muted);
        });
    }

    [Component]
    private void ProjectCard(UiContext cx, string name, string detail, string language, string status)
    {
        Ui.Grid(cx, "*,140,110").Padding(16).Border(1, Line).Radius(8).Children(() =>
        {
            Ui.Column(cx).Gap(6).Children(() =>
            {
                Ui.Text(cx, name).FontSize(15).Bold().TextColor(Ink);
                Ui.Text(cx, detail).FontSize(12).TextColor(Muted);
            });
            Ui.Text(cx, language).FontSize(12).TextColor(Muted);
            Ui.Text(cx, status).FontSize(12).TextColor(Blue);
        });
    }
}
