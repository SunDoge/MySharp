using MySharp;
using MySharp.Examples.Pulse;

public sealed partial class Workspace
{
    private readonly PulseModel pulse = new();
    [Component]
    private void PulsePage(UiContext cx, Action back) => Ui.Control(cx, () => new PulseView(pulse, back)).Fill();
}
