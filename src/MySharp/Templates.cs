using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Threading;

namespace MySharp;

public sealed partial class UiContext
{
    /// <summary>Owns a native data template. Row-local resources exist only while the row is visually attached.</summary>
    public UiDataTemplate<T> Template<T>(Action<UiContext, T> build, string? key = null,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(build);
        var template = Resource("template", key, file, line, build, static builder => new UiDataTemplate<T>(builder));
        template.Update(build);
        return template;
    }
}

/// <summary>A retained template whose attached rows own independent MySharp views.</summary>
public sealed class UiDataTemplate<T> : IDataTemplate, IDisposable
{
    private Action<UiContext, T>? build;
    private readonly HashSet<Row> attached = [];
    internal UiDataTemplate(Action<UiContext, T> build) => this.build = build;
    internal void Update(Action<UiContext, T> value)
    {
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(build is null, this);
        build = value;
        foreach (var row in attached) row.Refresh();
    }
    public bool Match(object? data) => data is T;
    public Control? Build(object? data)
    {
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(build is null, this);
        return data is T item ? new Row(this, item) : null;
    }
    public void Dispose()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (build is null) return;
        build = null;
        var errors = new CleanupErrors();
        foreach (var row in attached.ToArray())
            try { row.Release(); } catch (Exception error) { errors.Add(error); }
        attached.Clear();
        errors.ThrowIfAny();
    }
    private sealed class Row(UiDataTemplate<T> owner, T item) : ContentControl
    {
        private UiView? view;
        internal void Refresh() => view?.Invalidate();
        internal void Release()
        {
            var previous = view;
            view = null;
            owner.attached.Remove(this);
            try { Content = null; }
            finally { previous?.Dispose(); }
        }
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            if (owner.build is null || view is not null) return;
            var created = new UiView(cx => owner.build?.Invoke(cx, item));
            view = created;
            owner.attached.Add(this);
            try { Content = created; }
            catch { Release(); throw; }
        }
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            try { Release(); }
            finally { base.OnDetachedFromVisualTree(e); }
        }
    }
}
