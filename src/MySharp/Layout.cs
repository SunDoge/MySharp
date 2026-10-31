using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MySharp;

public readonly partial struct Element
{
    public Element MinWidth(double value) => Box(Layoutable.MinWidthProperty, value);
    public Element MinHeight(double value) => Box(Layoutable.MinHeightProperty, value);
    public Element MaxWidth(double value) => Box(Layoutable.MaxWidthProperty, value);
    public Element MaxHeight(double value) => Box(Layoutable.MaxHeightProperty, value);
    public Element Horizontal(HorizontalAlignment value) => Box(Layoutable.HorizontalAlignmentProperty, value);
    public Element Vertical(VerticalAlignment value) => Box(Layoutable.VerticalAlignmentProperty, value);
    public Element Opacity(double value) => Box(Visual.OpacityProperty, value);
    public Element Visible(bool value = true) => Box(Visual.IsVisibleProperty, value);
    public Element Tooltip(object? value) => Set(ToolTip.TipProperty, value);
    public Element Description(string value) => Set(Avalonia.Automation.AutomationProperties.HelpTextProperty, value);
    public Element Rows(string definitions)
    {
        var node = Node;
        var grid = node.Inner as Grid ?? throw new InvalidOperationException("Rows requires Grid.");
        node.RowsGeneration = generation;
        if (node.Rows != definitions) { grid.RowDefinitions = new RowDefinitions(definitions); node.Rows = definitions; }
        return this;
    }
    public Element Cell(int row, int column, int rowSpan = 1, int columnSpan = 1)
    {
        var node = Node;
        if (row < 0 || column < 0 || rowSpan < 1 || columnSpan < 1) throw new ArgumentOutOfRangeException(nameof(row));
        node.ExplicitCell = true;
        return Box(Grid.RowProperty, row).Box(Grid.ColumnProperty, column)
            .Box(Grid.RowSpanProperty, rowSpan).Box(Grid.ColumnSpanProperty, columnSpan);
    }
    public Element Dock(Avalonia.Controls.Dock value) => Box(DockPanel.DockProperty, value);
    public Element FontFamily(string value)
    {
        var node = Node;
        var font = new FontFamily(value);
        if (node.Inner is TextBlock) return Set(TextBlock.FontFamilyProperty, font);
        return Set(Avalonia.Controls.Primitives.TemplatedControl.FontFamilyProperty, font);
    }
    public Element Multiline(bool value = true)
    {
        Native<TextBox>();
        return Set(TextBox.AcceptsReturnProperty, value).Set(TextBox.TextWrappingProperty, value ? TextWrapping.Wrap : TextWrapping.NoWrap);
    }
    public Element Class(string name, bool enabled = true)
    {
        var node = Node;
        node.Classes[name] = generation;
        node.Inner.Classes.Set(name, enabled);
        return this;
    }
}
