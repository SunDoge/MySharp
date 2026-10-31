using MySharp;

public sealed partial class Workspace
{
    [Component]
    private void SettingsPage(UiContext cx)
    {
        cx.Scope(() => Heading(cx, "Make yourself at home.", "A few preferences for your local workspace."));
        Ui.Text(cx, "Profile").FontSize(19).Bold().TextColor(Ink);
        Ui.Column(cx).Gap(12).Padding(20).Border(1, Line).Radius(8).Children(() =>
        {
            Ui.Text(cx, "Display name").FontSize(12).Bold().TextColor(Ink);
            Ui.TextInput(cx, draftName, value => { draftName = value; savedMessage = ""; }).Placeholder("Your name").Label("Display name");
            Ui.Text(cx, "Workspace name").FontSize(12).Bold().TextColor(Ink);
            Ui.TextInput(cx, draftWorkspace, value => { draftWorkspace = value; savedMessage = ""; }).Placeholder("Workspace name").Label("Workspace name");
            Ui.Text(cx, "Shown in the sidebar. These values are kept for this session only.").FontSize(11)
                .TextColor(Muted);
        });
        Ui.Text(cx, "Preferences").FontSize(19).Bold().TextColor(Ink);
        cx.Scope(() => Preference(cx, "Recent activity", "Show recent activity on the overview page.", draftShowActivity,
            () => { draftShowActivity = !draftShowActivity; savedMessage = ""; }));
        cx.Scope(() => Preference(cx, "Compact task rows", "Use less vertical space in your task list.", draftCompact,
            () => { draftCompact = !draftCompact; savedMessage = ""; }));
        Ui.Row(cx).Gap(12).Children(() =>
        {
            Ui.Button(cx, "Save preferences").Icon(Icons.Save).Primary()
                .Disabled(string.IsNullOrWhiteSpace(draftName) || string.IsNullOrWhiteSpace(draftWorkspace))
                .OnClick(() =>
                {
                    displayName = draftName.Trim();
                    workspaceName = draftWorkspace.Trim();
                    showActivity = draftShowActivity;
                    compact = draftCompact;
                    savedMessage = "Preferences saved for this session.";
                });
            Ui.Text(cx, savedMessage).FontSize(12).TextColor(Blue);
        });
        Ui.Column(cx).Gap(8).Padding(20).Background(Surface).Radius(8).Children(() =>
        {
            Ui.Text(cx, "About this demo").FontSize(14).Bold().TextColor(Ink);
            Ui.Text(cx, $"MySharp · C# + Avalonia · Activity section: {(showActivity ? "on" : "off")}").FontSize(12)
                .TextColor(Muted);
            Ui.Text(cx, "Local sample data. Your changes last until you close the app.").FontSize(11).TextColor(Muted);
        });
    }

    [Component]
    private void Preference(UiContext cx, string title, string description, bool enabled, Action toggle)
    {
        Ui.Grid(cx, "*,100").Padding(20).Border(1, Line).Radius(8).Children(() =>
        {
            Ui.Column(cx).Gap(6).Children(() =>
            {
                Ui.Text(cx, title).FontSize(14).Bold().TextColor(Ink);
                Ui.Text(cx, description).FontSize(12).TextColor(Muted);
            });
            Ui.Switch(cx, "", enabled, _ => toggle()).Label(title);
        });
    }
}
