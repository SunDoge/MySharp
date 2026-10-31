using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using System.Runtime.CompilerServices;

namespace MySharp;

/// <summary>The containing window and native TopLevel services. Initialized when the view attaches.</summary>
public sealed class UiWindow : ObservableState
{
    private Window? native;
    public Window? Native { get { Verify(); return native; } }
    public WindowState State { get { Observe(); return native?.WindowState ?? WindowState.Normal; } }
    public bool CanMinimize { get { Observe(); return native?.CanMinimize ?? true; } }
    public bool CanMaximize { get { Observe(); return native?.CanMaximize ?? true; } }
    public bool IsActive { get { Observe(); return native?.IsActive == true; } }
    public string Title { get { Observe(); return native?.Title ?? ""; } }
    internal void Attach(Window? window)
    {
        if (Disposed || native == window) return;
        if (native is not null) native.PropertyChanged -= Changed;
        native = window;
        if (native is not null) native.PropertyChanged += Changed;
        Notify();
    }
    private void Changed(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property == Window.WindowStateProperty || args.Property == Window.IsActiveProperty || args.Property == Window.TitleProperty || args.Property == Window.CanMaximizeProperty || args.Property == Window.CanMinimizeProperty)
            Notify();
    }
    private Window Require() { Verify(); return native ?? throw new InvalidOperationException("The view is not attached to a Window."); }
    public void Minimize() => Require().WindowState = WindowState.Minimized;
    public void Maximize() => Require().WindowState = WindowState.Maximized;
    public void Restore() => Require().WindowState = WindowState.Normal;
    public void ToggleMaximize() { var window = Require(); window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
    public void Fullscreen() => Require().WindowState = WindowState.FullScreen;
    public void Activate() => Require().Activate();
    public void Hide() => Require().Hide();
    public void Show() => Require().Show();
    public void Close() => Require().Close();
    public void Close(object? result) => Require().Close(result);
    public void BeginMoveDrag(PointerPressedEventArgs args) => Require().BeginMoveDrag(args);
    public void BeginResizeDrag(WindowEdge edge, PointerPressedEventArgs args) => Require().BeginResizeDrag(edge, args);
    public void ShowOwned(Window window) => window.Show(Require());
    public Task<T> ShowDialog<T>(Window dialog) => dialog.ShowDialog<T>(Require());
    protected override void DisposeCore()
    {
        if (native is not null) native.PropertyChanged -= Changed;
        native = null;
    }
}

public readonly partial struct Element
{
    public Element TitleBar() => DecorationRole(WindowDecorationsElementRole.TitleBar);
    public Element DecorationRole(WindowDecorationsElementRole role) => Box(WindowDecorationProperties.ElementRoleProperty, role);
}

public static partial class Ui
{
    [Widget]
    public static Element WindowControls(UiContext cx, string? key = null, CallSiteId site = default,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Row(cx, key, site, file, line).Gap(2).DecorationRole(WindowDecorationsElementRole.User).Children(() =>
        {
            Button(cx, "—").Flat().Width(40).Label("Minimize").Disabled(!cx.Window.CanMinimize).OnClick(cx.Window.Minimize);
            Button(cx, cx.Window.State == WindowState.Maximized ? "❐" : "□").Flat().Width(40).Label("Maximize or restore").Disabled(!cx.Window.CanMaximize && cx.Window.State != WindowState.Maximized).OnClick(cx.Window.ToggleMaximize);
            Button(cx, "×").Flat().Width(40).Label("Close").OnClick(cx.Window.Close);
        });
}
