using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;

namespace MySharp;

public sealed partial class MySharpTheme
{
    private void ConfigureSegments()
    {
        Add(new Style(x => x.OfType<ListBox>().Class("segmented"))
        {
            Setters =
            {
                new Setter(Layoutable.HorizontalAlignmentProperty, HorizontalAlignment.Left),
                new Setter(TemplatedControl.BackgroundProperty, new DynamicResourceExtension("MySharpSurface")),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(3)),
                new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(7)),
                new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(1)),
                new Setter(TemplatedControl.BorderBrushProperty, new DynamicResourceExtension("MySharpBorder")),
                new Setter(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled),
                new Setter(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled)
            }
        });
        Add(new Style(x => x.OfType<ListBox>().Class("segmented").Descendant().OfType<ListBoxItem>())
        {
            Setters =
            {
                new Setter(Layoutable.MinHeightProperty, 28d),
                new Setter(Layoutable.MarginProperty, new Thickness(0)),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(12, 4)),
                new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(4)),
                new Setter(ContentControl.VerticalContentAlignmentProperty, VerticalAlignment.Center),
                new Setter(TemplatedControl.ForegroundProperty, new DynamicResourceExtension("MySharpTextMuted"))
            }
        });
        Add(new Style(x => x.OfType<ListBox>().Class("segmented").Descendant().OfType<ListBoxItem>().Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
        {
            Setters =
            {
                new Setter(ContentPresenter.BackgroundProperty, Brushes.Transparent),
                new Setter(ContentPresenter.BorderThicknessProperty, new Thickness(0))
            }
        });
        Add(new Style(x => x.OfType<ListBox>().Class("segmented").Descendant().OfType<ListBoxItem>().Class(":pointerover").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
        {
            Setters =
            {
                new Setter(ContentPresenter.BackgroundProperty, new DynamicResourceExtension("MySharpSurfaceHover")),
                new Setter(ContentPresenter.ForegroundProperty, new DynamicResourceExtension("MySharpText"))
            }
        });
        foreach (var state in new[] { "", ":pointerover", ":pressed" })
        {
            Add(new Style(x =>
            {
                var selector = x.OfType<ListBox>().Class("segmented").Descendant().OfType<ListBoxItem>().Class(":selected");
                if (state.Length != 0) selector = selector.Class(state);
                return selector.Template().OfType<ContentPresenter>().Name("PART_ContentPresenter");
            })
            {
                Setters =
                {
                    new Setter(ContentPresenter.BackgroundProperty, new DynamicResourceExtension("MySharpSegmentSelection")),
                    new Setter(ContentPresenter.ForegroundProperty, new DynamicResourceExtension("MySharpText")),
                    new Setter(ContentPresenter.BorderThicknessProperty, new Thickness(0))
                }
            });
        }
        Add(new Style(x => x.OfType<ListBox>().Class("segmented").Descendant().OfType<ListBoxItem>().Class(":disabled").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
        {
            Setters = { new Setter(ContentPresenter.ForegroundProperty, new DynamicResourceExtension("MySharpTextMuted")) }
        });
        Add(new Style(x => x.OfType<ListBox>().Class("segmented").Descendant().OfType<ListBoxItem>().Class(":selected").Class(":disabled").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
        {
            Setters = { new Setter(ContentPresenter.ForegroundProperty, new DynamicResourceExtension("MySharpTextMuted")) }
        });
    }
}
