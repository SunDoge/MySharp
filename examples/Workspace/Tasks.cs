using MySharp;

public sealed partial class Workspace
{
    [Component]
    private void TasksPage(UiContext cx)
    {
        cx.Scope(() => Heading(cx, "Make room for the next step.", "A short list for the work that matters. Changes stay in this session."));
        Ui.Grid(cx, "*,Auto").Children(() =>
        {
            Ui.TextInput(cx, draft, value => draft = value).Placeholder("What would you like to work on?").Label("New task");
            Ui.Button(cx, "Add task").Icon(Icons.Plus).Primary().Margin(3).Disabled(string.IsNullOrWhiteSpace(draft)).OnClick(() =>
            {
                tasks.Add(new WorkItem(nextId++, draft.Trim(), "MySharp", "Today", false));
                draft = "";
            });
        });
        Ui.Row(cx).Gap(8).Children(() =>
        {
            cx.Scope(() => FilterButton(cx, "All"));
            cx.Scope(() => FilterButton(cx, "Open"));
            cx.Scope(() => FilterButton(cx, "Completed"));
        });
        Ui.Grid(cx, "*,120,100").Padding(16, 0).Children(() =>
        {
            Ui.Text(cx, "TASK").FontSize(10).TextColor(Muted);
            Ui.Text(cx, "PROJECT").FontSize(10).TextColor(Muted);
            Ui.Text(cx, "DUE").FontSize(10).TextColor(Muted);
        });
        var visible = tasks.Where(t => filter == "All" || (filter == "Completed" ? t.Done : !t.Done)).ToArray();
        Ui.Column(cx).Gap(10).Children(() =>
        {
            cx.Scope(() =>
            {
                foreach (var item in visible) cx.Scope(item.Id, () => TaskRow(cx, item));
            });
            if (visible.Length == 0)
                Ui.Text(cx, "Nothing here yet. Add a task or choose another filter.").Padding(24).TextColor(Muted);
        });
        Ui.Grid(cx, "*,180").Children(() =>
        {
            Ui.Text(cx, $"{tasks.Count(t => !t.Done)} open · {tasks.Count(t => t.Done)} completed").FontSize(12).TextColor(Muted);
            Ui.Button(cx, "Clear completed").Icon(Icons.Trash).Flat().Disabled(!tasks.Any(t => t.Done))
                .OnClick(() => tasks.RemoveAll(t => t.Done));
        });
        Ui.Column(cx).Gap(8).Padding(20).Background(Surface).Radius(8).Children(() =>
        {
            Ui.Text(cx, "One thing at a time.").FontSize(15).Bold().TextColor(Ink);
            Ui.Text(cx, "Click a checkbox or its task name to mark it complete.").FontSize(12).TextColor(Muted);
        });
    }

    [Component]
    private void FilterButton(UiContext cx, string value)
    {
        Ui.Button(cx, value).Flat().Background(filter == value ? Selection : Surface).Radius(6)
            .TextColor(filter == value ? Blue : Muted).OnClick(() => filter = value);
    }

    [Component]
    private void TaskRow(UiContext cx, WorkItem item)
    {
        Ui.Grid(cx, "44,*,120,100").Padding(16, compact ? 8 : 16).Border(1, Line).Radius(8).Children(() =>
        {
            Ui.Checkbox(cx, item.Title, item.Done, value => item.Done = value).Cell(0, 0, 1, 2)
                .FontSize(13).TextColor(item.Done ? Muted : Ink).Vertical(Avalonia.Layout.VerticalAlignment.Center);
            Ui.Text(cx, item.Project).Cell(0, 2).FontSize(12).TextColor(Muted).Vertical(Avalonia.Layout.VerticalAlignment.Center);
            Ui.Text(cx, item.Done ? "Done" : item.Due).Cell(0, 3).FontSize(12).TextColor(item.Done ? Blue : Muted)
                .Vertical(Avalonia.Layout.VerticalAlignment.Center);
        });
    }
}
