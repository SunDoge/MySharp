using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;

namespace MySharp;

public sealed partial class UiContext
{
    /// <summary>Owns a retained flyout. Content-local state exists only while the flyout is visually attached.</summary>
    public UiFlyout Flyout(Action<UiContext> build, string? key = null,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(build);
        var flyout = Resource("flyout", key, file, line, build, static builder => new UiFlyout(builder));
        flyout.Update(build);
        return flyout;
    }
}

public sealed class UiFlyout : IDisposable
{
    private Action<UiContext>? build;
    private readonly UiDataTemplate<byte> content;
    private readonly Action<UiContext, byte> render;
    public Flyout Native { get; }
    internal UiFlyout(Action<UiContext> build)
    {
        this.build = build;
        render = (cx, _) => this.build?.Invoke(cx);
        content = new UiDataTemplate<byte>(render);
        Native = new Flyout { Content = content.Build((byte)0) };
    }
    internal void Update(Action<UiContext> value)
    {
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(build is null, this);
        build = value;
        content.Update(render);
    }
    public void ShowAt(Control target)
    {
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(build is null, this);
        Native.ShowAt(target);
    }
    public void Hide()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (build is not null) Native.Hide();
    }
    public void Dispose()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (build is null) return;
        build = null;
        var errors = new CleanupErrors();
        try { Native.Hide(); } catch (Exception error) { errors.Add(error); }
        try { content.Dispose(); } catch (Exception error) { errors.Add(error); }
        try { Native.Content = null; } catch (Exception error) { errors.Add(error); }
        errors.ThrowIfAny();
    }
}

public readonly partial struct Element
{
    /// <summary>Attaches a native context menu; replacement, omission or unmount closes it. Content ownership stays with the caller.</summary>
    public Element ContextMenu(ContextMenu? menu) => Overlay(Control.ContextMenuProperty, menu);
    public Element ContextFlyout(FlyoutBase? flyout) => Overlay(Control.ContextFlyoutProperty, flyout);
    /// <summary>Attaches a flyout opened by a native Button. Use cx.Flyout to own MySharp content.</summary>
    public Element Flyout(FlyoutBase? flyout)
    {
        Native<Button>();
        return Overlay(Button.FlyoutProperty, flyout);
    }
    public Element Flyout(UiFlyout flyout) => Flyout(flyout.Native);
    public Element ContextFlyout(UiFlyout flyout) => ContextFlyout(flyout.Native);
    private Element Overlay<T>(StyledProperty<T?> property, T? value) where T : class
    {
        var node = Node;
        OverlaySubscription? slot = null;
        foreach (var candidate in node.Subscriptions)
            if (candidate is OverlaySubscription overlay && ReferenceEquals(overlay.Key, property)) { slot = overlay; break; }
        if (slot is null)
        {
            slot = new OverlaySubscription(property);
            node.Subscriptions.Add(slot);
        }
        slot.Use(value); slot.Seen = generation;
        return Set(property, value);
    }
}

internal sealed class OverlaySubscription(object key) : NativeSubscription(key)
{
    private object? value;
    internal void Use(object? next)
    {
        if (ReferenceEquals(value, next)) return;
        Dispose(); value = next; Attach();
    }
    internal override void Attach() => Attached = true;
    public override void Dispose()
    {
        var previous = value;
        value = null; Attached = false;
        if (previous is ContextMenu menu) menu.Close();
        else if (previous is FlyoutBase flyout) flyout.Hide();
    }
}
