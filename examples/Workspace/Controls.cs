using MySharp;

public sealed partial class Workspace
{
    private static readonly string[] ExportFormats = ["PNG image", "JPEG image", "WebP image"];
    private static readonly string[] PreviewModes = ["Original", "Translated"];

    [Component]
    private void ControlsPage(UiContext cx)
    {
        var format = cx.State(0);
        var includeText = cx.State(true);
        var automatic = cx.State(true);
        var mode = cx.State(false);
        var preview = cx.State(0);
        var quality = cx.State(75d);
        var copies = cx.State<decimal?>(1);
        var name = cx.State("");
        var notice = cx.State("Ready to export");
        var details = cx.State(false);
        var note = cx.State("");
        var date = cx.State<DateTimeOffset?>(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var time = cx.State<TimeSpan?>(TimeSpan.FromHours(9));
        var options = cx.Flyout(body => Ui.Column(body).Gap(10).Children(() =>
        {
            Ui.Text(body, "Export options").FontSize(16).Bold();
            Ui.Text(body, "The original image is always preserved.").FontSize(13);
            Ui.Text(body, $"Quality: {quality.Value:F0}%").FontSize(13).TextColor(Muted);
        }));
        cx.Scope(() => Heading(cx, "Default components", "Shared styles, without per-control color or size overrides."));
        Ui.Grid(cx, "*,*").Set(Avalonia.Controls.Grid.ColumnSpacingProperty, 20d).Children(() =>
        {
            Ui.Column(cx).Gap(16).Padding(20).Border(1, Line).Radius(8).Children(() =>
            {
                Ui.Text(cx, "Actions").FontSize(17).Bold();
                Ui.Text(cx, "One primary action, quiet secondary tools.").FontSize(12).TextColor(Muted);
                Ui.Row(cx).Gap(8).Children(() =>
                {
                    Ui.Button(cx, "Export image").Icon(Icons.Download).Primary().OnClick(() => notice.Value = "Image exported");
                    Ui.Button(cx, "Save draft").Icon(Icons.Save).OnClick(() => notice.Value = "Draft saved");
                });
                Ui.Row(cx).Gap(8).Children(() =>
                {
                    Ui.IconButton(cx, Icons.More, "More options").Class("quiet").Flyout(options).Tooltip("Additional export options");
                    Ui.Button(cx, "Unavailable").Disabled(true);
                });
                Ui.Segmented(cx, PreviewModes, preview).Label("Preview mode");
            });
            Ui.Column(cx).Gap(12).Padding(20).Border(1, Line).Radius(8).Children(() =>
            {
                Ui.Text(cx, "Inputs").FontSize(17).Bold();
                Ui.TextInput(cx, name).Placeholder("Image name").Label("Image name");
                Ui.Select(cx, ExportFormats, format).Label("Export format");
                Ui.NumberInput(cx, copies.Value, value => copies.Value = value, 1, 100).Label("Copies");
                Ui.Text(cx, "Tab through fields to inspect keyboard focus.").FontSize(12).TextColor(Muted);
            });
        });
        Ui.Grid(cx, "*,*").Set(Avalonia.Controls.Grid.ColumnSpacingProperty, 20d).Children(() =>
        {
            Ui.Column(cx).Gap(12).Padding(20).Border(1, Line).Radius(8).Children(() =>
            {
                Ui.Text(cx, "Selection").FontSize(17).Bold();
                Ui.Checkbox(cx, "Include translated text", includeText);
                Ui.Checkbox(cx, "Original image is preserved", true, _ => { }).Disabled(true);
                Ui.Row(cx).Gap(16).Children(() =>
                {
                    Ui.Radio(cx, "Current page", "export-pages", !mode.Value, value => { if (value) mode.Value = false; });
                    Ui.Radio(cx, "All pages", "export-pages", mode.Value, value => { if (value) mode.Value = true; });
                });
                Ui.Switch(cx, "Automatic processing", automatic.Value, value => automatic.Value = value);
            });
            Ui.Column(cx).Gap(12).Padding(20).Border(1, Line).Radius(8).Children(() =>
            {
                Ui.Text(cx, "Progress & quality").FontSize(17).Bold();
                Ui.Text(cx, $"Image quality · {quality.Value:F0}%").FontSize(13).TextColor(Muted);
                Ui.Slider(cx, quality).Label("Image quality");
                Ui.Progress(cx, quality.Value);
                Ui.Text(cx, notice.Value).FontSize(13).TextColor(Blue);
                Ui.Text(cx, "Hover, press and focus each control. The same palette also applies to menus and popups.")
                    .FontSize(12).Wrap().TextColor(Muted);
            });
        });
        cx.Scope(() => CollectionControls(cx));
        Ui.Expander(cx, "Export details", details.Value, value => details.Value = value).Children(() =>
            Ui.Column(cx).Gap(8).Padding(12).Children(() =>
            {
                Ui.Text(cx, "Optional note").FontSize(13).TextColor(Muted);
                Ui.TextInput(cx, note).Label("Export note").Placeholder("Add a note");
                Ui.Grid(cx, "*,*").Set(Avalonia.Controls.Grid.ColumnSpacingProperty, 12d).Children(() =>
                {
                    Ui.Column(cx).Gap(6).Children(() =>
                    {
                        Ui.Text(cx, "Date").FontSize(13).TextColor(Muted);
                        Ui.DateInput(cx, date.Value, value => date.Value = value).Label("Example date");
                    });
                    Ui.Column(cx).Cell(0, 1).Gap(6).Children(() =>
                    {
                        Ui.Text(cx, "Time").FontSize(13).TextColor(Muted);
                        Ui.TimeInput(cx, time.Value, value => time.Value = value).Label("Example time")
                            .Set(Avalonia.Controls.TimePicker.ClockIdentifierProperty, "24HourClock");
                    });
                });
                Ui.Text(cx, "Collapsing this section preserves the draft. Use Tab and Space to open it with the keyboard.")
                    .FontSize(12).Wrap().TextColor(Muted);
            }));
    }
}
