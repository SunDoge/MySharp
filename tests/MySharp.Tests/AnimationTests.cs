using System.Diagnostics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using MySharp;

internal static class AnimationTests
{
    internal static int Run()
    {
        TextBlock? text = null;
        using var view = new UiView(cx => text = Ui.Text(cx, "Animated")
            .Set(Visual.OpacityProperty, 0.7).Native<TextBlock>());
        view.ProfilingEnabled = true;
        var window = new Window { Width = 300, Height = 200, Content = view };
        using var cancellation = new CancellationTokenSource();
        var animation = new Animation
        {
            Duration = TimeSpan.FromSeconds(2),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Visual.OpacityProperty, 0.0) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Visual.OpacityProperty, 1.0) } }
            }
        };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var playing = animation.RunAsync(text!, cancellation.Token);
            var timeout = Stopwatch.StartNew();
            do
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
                if (text!.Opacity > 0 && text.Opacity < 0.6) break;
                Thread.Sleep(5);
            } while (timeout.Elapsed.TotalSeconds < 1.5);
            if (playing.IsCompleted || text!.Opacity <= 0 || text.Opacity >= 0.6)
                throw new Exception("Expected a real in-progress native animation.");
            for (var frame = 0; frame < 20; frame++)
            {
                view.Rebuild();
                if (view.LastProfile.PropertyWrites != 0)
                    throw new Exception("An unchanged declaration must compare the base value, not rewrite the animated effective value.");
            }
            if (text.GetBaseValue(Visual.OpacityProperty).Value != 0.7 || text.Opacity >= 0.7)
                throw new Exception("Native animation and declarative base value must coexist.");
            cancellation.Cancel();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
            if (text.Opacity != 0.7) throw new Exception("Canceling native animation must reveal the declarative base value.");
        }
        finally { cancellation.Cancel(); window.Close(); }
        Console.WriteLine("PASS native animation survives unchanged declarations without property rewrites");
        return 1;
    }
}
