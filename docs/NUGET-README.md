# MySharp

Pure C# UI composition on Avalonia. Write ordinary C# functions and fluent calls; MySharp retains native controls and updates the UI on demand. No XAML, custom language or source generator is required.

Package family:

- `SunDoge.MySharp`: core UI context, controls, state, tasks and explicit resource ownership.
- `SunDoge.MySharp.Desktop`: desktop window host, Fluent-based theme and Inter font integration.
- `SunDoge.MySharp.HotReload`: optional development helper, excluded from normal Release applications.

All packages use the `MySharp` C# namespace. This preview targets .NET 10 and Avalonia 12.1.4. The GitHub source release does not itself imply these packages have been published to NuGet.org.

```csharp
using MySharp;

class Program
{
    [STAThread]
    static void Main() => MySharpApp.Run(Build);

    static void Build(UiContext cx)
    {
        var count = cx.State(0);
        Ui.Column(cx).Padding(24).Gap(12).Children(() =>
        {
            Ui.Text(cx, $"Count: {count.Value}");
            Ui.Button(cx, "Increment").OnClick(() => count.Value++);
        });
    }
}
```

Node identity normally follows caller file/line and parent scope. Use `cx.Scope(item.Id, ...)` for dynamic lists; moving declarations can reset local UI state. Native Avalonia APIs remain accessible. Hot Reload is best effort; arbitrary structural edits/state migration are not guaranteed. Linux X11 has native validation; Windows/macOS require further native coverage.

[Source, documentation and examples](https://github.com/SunDoge/MySharp) · [Issues](https://github.com/SunDoge/MySharp/issues)

Licensed under Apache-2.0. Dependencies retain their own licenses; see the repository's third-party notices.
