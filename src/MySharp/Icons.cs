using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Lucide.Avalonia;

namespace MySharp;

/// <summary>Common Lucide icons. All other icons are available through LucideIconKind.</summary>
public static class Icons
{
    public const LucideIconKind Home = LucideIconKind.House;
    public const LucideIconKind Tasks = LucideIconKind.ListTodo;
    public const LucideIconKind Settings = LucideIconKind.Settings;
    public const LucideIconKind Components = LucideIconKind.Component;
    public const LucideIconKind Activity = LucideIconKind.Activity;
    public const LucideIconKind Plus = LucideIconKind.Plus;
    public const LucideIconKind Trash = LucideIconKind.Trash;
    public const LucideIconKind Search = LucideIconKind.Search;
    public const LucideIconKind ArrowRight = LucideIconKind.ArrowRight;
    public const LucideIconKind Download = LucideIconKind.Download;
    public const LucideIconKind Save = LucideIconKind.Save;
    public const LucideIconKind More = LucideIconKind.Ellipsis;
    public const LucideIconKind X = LucideIconKind.X;
}

public static partial class Ui
{
    /// <summary>A retained vector icon, using the inherited foreground color.</summary>
    [Widget]
    public static Element Icon(UiContext cx, LucideIconKind icon, double size = 16, string? key = null,
        CallSiteId site = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Control<LucideIcon>(cx, key, site, file, line).Set<LucideIconKind?>(LucideIcon.KindProperty, icon)
            .Set(LucideIcon.SizeProperty, IconContent.ValidateSize(size));

    /// <summary>An icon-only button with a required accessible name and tooltip.</summary>
    [Widget]
    public static Element IconButton(UiContext cx, LucideIconKind icon, string label, string? key = null,
        CallSiteId site = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        return Button(cx, "", key, site, file, line).Icon(icon).Class("icon-only").Label(label).Tooltip(label);
    }
}

public readonly partial struct Element
{
    /// <summary>Adds a leading icon without replacing the button's string content.</summary>
    public Element Icon(LucideIconKind icon, double size = 16)
    {
        Native<Button>();
        return Set<LucideIconKind?>(IconContent.KindProperty, icon)
            .Set(IconContent.SizeProperty, IconContent.ValidateSize(size))
            .Set<IDataTemplate?>(ContentControl.ContentTemplateProperty, IconContent.Template);
    }
}

internal sealed class IconContent : StackPanel
{
    // Inherited attached properties avoid reflection-based bindings or per-build
    // templates. Native template recycling retains the icon when text changes.
    internal static readonly AttachedProperty<LucideIconKind?> KindProperty =
        AvaloniaProperty.RegisterAttached<IconContent, StyledElement, LucideIconKind?>("Kind", inherits: true);
    internal static readonly AttachedProperty<double> SizeProperty =
        AvaloniaProperty.RegisterAttached<IconContent, StyledElement, double>("IconSize", 16, inherits: true);
    internal static readonly IDataTemplate Template = new FuncDataTemplate<string>((_, _) => new IconContent(), true);
    private readonly TextBlock label = new() { VerticalAlignment = VerticalAlignment.Center };

    public IconContent()
    {
        Orientation = Orientation.Horizontal;
        VerticalAlignment = VerticalAlignment.Center;
        IsHitTestVisible = false;
        var icon = new LucideIcon { VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        icon.Bind(LucideIcon.KindProperty, icon.GetObservable(KindProperty));
        icon.Bind(LucideIcon.SizeProperty, icon.GetObservable(SizeProperty));
        Children.Add(icon); Children.Add(label);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        label.Text = DataContext as string;
        label.IsVisible = !string.IsNullOrEmpty(label.Text);
        Spacing = label.IsVisible ? 8 : 0;
    }

    internal static double ValidateSize(double size) => double.IsFinite(size) && size > 0
        ? size : throw new ArgumentOutOfRangeException(nameof(size), "Icon size must be finite and positive.");
}
