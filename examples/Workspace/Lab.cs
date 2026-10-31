using MySharp;
using Avalonia.Input;

public sealed partial class Workspace
{
    private static readonly KeyGesture LoadShortcut = new(Key.L, KeyModifiers.Control);
    private static readonly string[] DensityOptions = ["Balanced", "Compact", "Comfortable"];
    [Component]
    private void LabPage(UiContext cx)
    {
        var count = cx.State(0);
        var enabled = cx.State(true);
        var amount = cx.State(35.0);
        var choice = cx.State(0);
        var query = cx.State("MySharp");
        var job = cx.Task<string>();
        cx.Scope(() => Heading(cx, "State & async work", "Try native controls and cancellable tasks. Leaving this page clears its local state."));
        var details = cx.Flyout(body =>
        {
            var openings = body.State(0);
            Ui.Column(body).Width(260).Gap(8).Children(() =>
            {
                Ui.Text(body, "Owned flyout").FontSize(18).Bold();
                Ui.Text(body, $"Current amount: {amount.Value:F0}").TextColor(Muted);
                Ui.Button(body, $"Popup count: {openings.Value}").OnClick(() => openings.Value++);
                Ui.Text(body, "This count resets when the popup closes.").FontSize(12).Wrap().TextColor(Muted);
            });
        });
        Ui.Row(cx).Gap(12).Children(() =>
        {
            Ui.Button(cx, $"Local count: {count.Value}").OnClick(() => count.Value++);
            Ui.Button(cx, $"Shared count: {sharedClicks.Value}").OnClick(() => sharedClicks.Value++);
            Ui.Button(cx, "Open details").Class("quiet").Flyout(details).Tooltip("Popup-local state is released on close.");
        });
        Ui.Checkbox(cx, "Enable local controls", enabled);
        Ui.Slider(cx, amount).Disabled(!enabled.Value).Label("Amount");
        Ui.Text(cx, $"Amount: {amount.Value:F0}").TextColor(Muted);
        Ui.Select(cx, DensityOptions, choice).Disabled(!enabled.Value).Label("Density preset");
        Ui.Text(cx, "Manual background task").FontSize(18).Bold();
        Ui.Row(cx).Gap(8).Children(() =>
        {
            Ui.Button(cx, "Load sample").Primary().HotKey(LoadShortcut).Tooltip("Ctrl+L").Disabled(job.IsRunning).OnClick(() =>
            {
                var snapshot = query.Value;
                job.Run(async token =>
                {
                    await Task.Delay(600, token);
                    return $"Loaded: {snapshot}";
                });
            });
            Ui.Button(cx, "Cancel").Disabled(!job.IsRunning).OnClick(job.Cancel);
            Ui.Button(cx, "Simulate error").Disabled(job.IsRunning).OnClick(() =>
                job.Run(_ => Task.FromException<string>(new InvalidOperationException("Sample request failed. Try Load sample again."))));
        });
        Ui.Progress(cx, job.HasValue ? 100 : 0, job.IsRunning).Height(5);
        Ui.Text(cx, job.IsRunning ? "Loading…" : job.HasValue ? job.Result! : job.Error?.Message ?? job.Status.ToString())
            .FontSize(13).TextColor(job.Error is null ? Blue : Error).Wrap();
        Ui.Text(cx, "Automatic preview").FontSize(18).Bold();
        Ui.Text(cx, "Editing the query cancels the old preview; late results are ignored.").FontSize(12)
            .TextColor(Muted);
        Ui.TextInput(cx, query).Label("Preview query");
        var preview = cx.Task(query.Value, async (string text, CancellationToken token) =>
        {
            await Task.Delay(250, token);
            return text.ToUpperInvariant();
        });
        Ui.Text(cx, preview.IsRunning ? "Updating preview…" : preview.Result ?? "No preview").FontSize(16)
            .TextColor(Blue);
    }
}
