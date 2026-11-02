using MySharp;

public sealed partial class Workspace
{
    [Component]
    private void SettingsPage(UiContext cx)
    {
        var t = cx.UseI18n(translations);
        cx.Scope(() => Heading(cx, t["settings.title"], t["settings.subtitle"]));
        Ui.Text(cx, t["language"]).FontSize(19).Bold().TextColor(Ink);
        Ui.Segmented(cx, new[] { "English", "简体中文" }, t.Culture.Name == "zh-CN" ? 1 : 0,
            index => t.Culture = System.Globalization.CultureInfo.GetCultureInfo(index == 1 ? "zh-CN" : "en-US")).Label(t["language"]);
        Ui.Text(cx, t["language.hint"]).Wrap().FontSize(12).TextColor(Muted);
        Ui.Text(cx, t.Format("format.example", 12345.67, new DateTime(2026, 11, 2))).FontSize(12).TextColor(Muted);
        Ui.Text(cx, t["profile"]).FontSize(19).Bold().TextColor(Ink);
        Ui.Column(cx).Gap(12).Padding(20).Border(1, Line).Radius(8).Children(() =>
        {
            Ui.Text(cx, t["displayName"]).FontSize(12).Bold().TextColor(Ink);
            Ui.TextInput(cx, draftName, value => { draftName = value; savedMessage = ""; }).Placeholder(t["name.placeholder"]).Label(t["displayName"]);
            Ui.Text(cx, t["workspaceName"]).FontSize(12).Bold().TextColor(Ink);
            Ui.TextInput(cx, draftWorkspace, value => { draftWorkspace = value; savedMessage = ""; }).Placeholder(t["workspaceName"]).Label(t["workspaceName"]);
            Ui.Text(cx, t["profile.hint"]).FontSize(12).TextColor(Muted);
        });
        Ui.Text(cx, t["preferences"]).FontSize(19).Bold().TextColor(Ink);
        cx.Scope(() => Preference(cx, t["activity"], t["activity.hint"], draftShowActivity,
            () => { draftShowActivity = !draftShowActivity; savedMessage = ""; }));
        cx.Scope(() => Preference(cx, t["compact"], t["compact.hint"], draftCompact,
            () => { draftCompact = !draftCompact; savedMessage = ""; }));
        Ui.Row(cx).Gap(12).Children(() =>
        {
            Ui.Button(cx, t["save"]).Icon(Icons.Save).Primary()
                .Disabled(string.IsNullOrWhiteSpace(draftName) || string.IsNullOrWhiteSpace(draftWorkspace))
                .OnClick(() =>
                {
                    displayName = draftName.Trim();
                    workspaceName = draftWorkspace.Trim();
                    showActivity = draftShowActivity;
                    compact = draftCompact;
                    savedMessage = "saved";
                });
            Ui.Text(cx, savedMessage.Length == 0 ? "" : t["saved"]).FontSize(12).TextColor(Blue);
        });
        Ui.Column(cx).Gap(8).Padding(20).Background(Surface).Radius(8).Children(() =>
        {
            Ui.Text(cx, t["about"]).FontSize(14).Bold().TextColor(Ink);
            Ui.Text(cx, t.Format("about.detail", t[showActivity ? "on" : "off"])).FontSize(12).TextColor(Muted);
            Ui.Text(cx, t["local.hint"]).FontSize(12).TextColor(Muted);
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
