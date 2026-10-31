using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace MySharp;

public enum WindowChrome { System, Custom, Borderless }

public sealed record WindowOptions
{
    public string Title { get; init; } = "MySharp";
    public double Width { get; init; } = 960;
    public double Height { get; init; } = 680;
    public double MinWidth { get; init; } = 640;
    public double MinHeight { get; init; } = 480;
    public double MaxWidth { get; init; } = double.PositiveInfinity;
    public double MaxHeight { get; init; } = double.PositiveInfinity;
    public ThemeVariant Theme { get; init; } = ThemeVariant.Default;
    public WindowChrome Chrome { get; init; } = WindowChrome.System;
    public double TitleBarHeight { get; init; } = 44;
    public bool CanResize { get; init; } = true;
    public bool CanMinimize { get; init; } = true;
    public bool CanMaximize { get; init; } = true;
    public bool Topmost { get; init; }
    public bool ShowInTaskbar { get; init; } = true;
    public WindowState State { get; init; } = WindowState.Normal;
    public WindowStartupLocation StartupLocation { get; init; } = WindowStartupLocation.Manual;
    public PixelPoint? Position { get; init; }
    public WindowIcon? Icon { get; init; }
    public Action<Window>? ConfigureWindow { get; init; }
    public Action<WindowClosingEventArgs>? Closing { get; init; }
    public Action<Window>? Opened { get; init; }
    public Action<Window>? Closed { get; init; }
}

public static class MySharpApp
{
    /// <summary>Call on the main thread from an STAThread entry point. Configure hooks retain access to Avalonia platform/lifetime APIs.</summary>
    public static void Run(Action<UiContext> build, WindowOptions? options = null, string[]? args = null,
        Action<AppBuilder>? configureApp = null, Action<Application>? configureApplication = null,
        Action<IClassicDesktopStyleApplicationLifetime>? configureLifetime = null)
    {
        ArgumentNullException.ThrowIfNull(build);
        var builder = AppBuilder.Configure(() => new DesktopApplication(build, options ?? new WindowOptions(), configureApplication, configureLifetime))
            .UsePlatformDetect().WithInterFont().LogToTrace();
        configureApp?.Invoke(builder);
        builder.StartWithClassicDesktopLifetime(args ?? []);
    }

    /// <summary>Creates an additional owned, modeless or modal window after Avalonia initialization.</summary>
    public static Window CreateWindow(Action<UiContext> build, WindowOptions? options = null)
    {
        Dispatcher.UIThread.VerifyAccess();
        options ??= new WindowOptions();
        var view = new UiView(build);
        var window = new Window
        {
            Title = options.Title, Width = options.Width, Height = options.Height,
            MinWidth = options.MinWidth, MinHeight = options.MinHeight,
            MaxWidth = options.MaxWidth, MaxHeight = options.MaxHeight,
            RequestedThemeVariant = options.Theme, Content = view,
            WindowDecorations = options.Chrome == WindowChrome.System ? WindowDecorations.Full : WindowDecorations.None,
            ExtendClientAreaToDecorationsHint = options.Chrome == WindowChrome.Custom,
            ExtendClientAreaTitleBarHeightHint = options.TitleBarHeight,
            CanResize = options.CanResize, CanMinimize = options.CanMinimize, CanMaximize = options.CanMaximize,
            Topmost = options.Topmost, ShowInTaskbar = options.ShowInTaskbar,
            WindowState = options.State, WindowStartupLocation = options.StartupLocation, Icon = options.Icon
        };
        if (options.Position is { } position) window.Position = position;
        window.Closed += (_, _) => { view.Dispose(); options.Closed?.Invoke(window); };
        try { options.ConfigureWindow?.Invoke(window); }
        catch { window.Close(); view.Dispose(); throw; }
        if (options.Closing is { } closing) window.Closing += (_, args) => closing(args);
        if (options.Opened is { } opened) window.Opened += (_, _) => opened(window);
        return window;
    }

    private sealed class DesktopApplication(Action<UiContext> build, WindowOptions options, Action<Application>? configure,
        Action<IClassicDesktopStyleApplicationLifetime>? lifetime) : Application
    {
        public override void Initialize()
        {
            Styles.Add(MySharpTheme.CreateFluentBase());
            var theme = new MySharpTheme();
            Styles.Add(theme);
            configure?.Invoke(this);
        }
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = CreateWindow(build, options);
                lifetime?.Invoke(desktop);
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
