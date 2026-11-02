using MySharp;
using MySharp.Examples.Game2048;

public sealed partial class Workspace
{
    private Game2048Session? game2048;
    [Component]
    private void GamePage(UiContext cx)
    {
        game2048 ??= new Game2048Session(gameSavePath, reducedMotion: reducedMotion);
        Ui.Control(cx, () => new Game2048View(game2048)).Fill();
    }
}
