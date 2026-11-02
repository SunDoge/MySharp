using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lucide.Avalonia;

namespace MySharp.Examples.Pulse;

public sealed partial class PulseView : UserControl, IDisposable
{
    private readonly PulseModel model;
    private readonly Action back;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Grid body = new();
    private readonly StackPanel navigation = new() { Spacing = 3 };
    private readonly TextBlock status = Text("", 11, "#8C8C91");
    private readonly List<Action> updates = [];
    private readonly List<Action> navigationUpdates = [];
    private PulseSection shown = (PulseSection)(-1);
    private bool disposed;
    public PulseModel Model => model;
    public bool TimerRunning => timer.IsEnabled;
    public PulseView(PulseModel model, Action back)
    {
        this.model = model; this.back = back;
        Background = Brushes.White;
        Foreground = Brush.Parse("#171719");
        var shell = new Grid { ColumnDefinitions = new ColumnDefinitions("204,*") };
        var sidebar = new DockPanel { Background = Brush.Parse("#F4F4F6"), Margin = new Thickness(0) };
        var footer = Stack(Text("Demo workstation", 13, bold: true), Text("32 GB · Apple M5 sample", 11, "#8C8C91"), Button("← Back to Gallery", back));
        footer.Margin = new Thickness(16); DockPanel.SetDock(footer, Dock.Bottom); sidebar.Children.Add(footer);
        var navScroll = new ScrollViewer { Content = navigation, Padding = new Thickness(10, 18) };
        sidebar.Children.Add(navScroll); shell.Children.Add(sidebar);
        var right = new Grid { RowDefinitions = new RowDefinitions("*,28") };
        Grid.SetColumn(right, 1); right.Children.Add(body);
        status.Margin = new Thickness(24, 5); Grid.SetRow(status, 1); right.Children.Add(status); shell.Children.Add(right);
        Content = new Border { Child = new ThemeVariantScope { RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light, Child = shell } };
        HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Stretch;
        timer.Tick += Tick; model.Changed += Refresh; Refresh();
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    { base.OnAttachedToVisualTree(e); if (!model.Paused && !disposed) timer.Start(); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    { timer.Stop(); base.OnDetachedFromVisualTree(e); }
    private void Tick(object? sender, EventArgs e) => model.Tick();
    private void Refresh()
    {
        if (disposed) return;
        if (model.Paused) timer.Stop(); else if (VisualRoot is not null) timer.Start();
        status.Text = model.Status;
        if (shown != model.Section) { shown = model.Section; BuildNavigation(); BuildPage(); }
        foreach (var update in navigationUpdates) update();
        foreach (var update in updates.ToArray()) update();
    }
    private void BuildNavigation()
    {
        navigation.Children.Clear(); navigationUpdates.Clear();
        foreach (var section in Enum.GetValues<PulseSection>())
        {
            if (section is PulseSection.Cpu or PulseSection.Apps)
            { var label = Text(section == PulseSection.Cpu ? "Hardware" : "System", 11, "#8C8C91"); label.Margin = new Thickness(10, 22, 0, 7); navigation.Children.Add(label); }
            var active = section == model.Section;
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("26,*,Auto") };
            var icon = new LucideIcon { Kind = Icon(section), Size = 15, Foreground = active ? Brushes.White : Brush.Parse(ColorFor(section)) };
            row.Children.Add(icon); var text = Text(Title(section), 13, active ? "#FFFFFF" : "#343438"); Grid.SetColumn(text, 1); row.Children.Add(text);
            var value = Text(section switch { PulseSection.Processes => "24", PulseSection.Cpu => "59%", PulseSection.Memory => "84%", PulseSection.Gpu => "8.1%", PulseSection.Disk => "310 KB/s", PulseSection.Network => "2.41 MB/s", PulseSection.Energy => "80%", _ => "" }, 11, active ? "#FFFFFF" : "#8C8C91");
            navigationUpdates.Add(() => value.Text = section switch
            {
                PulseSection.Processes => model.Processes.Count.ToString(), PulseSection.Connections => model.Connections.Count.ToString(),
                PulseSection.Cpu => $"{model.Cpu:0}%", PulseSection.Memory => $"{model.UsedMemory.Ago(0):0}%",
                PulseSection.Gpu => $"{model.Gpu.Ago(0):0.0}%", PulseSection.Disk => $"{model.DiskRead.Ago(0) + model.DiskWrite.Ago(0):0} KB/s",
                PulseSection.Network => $"{model.Download.Ago(0) + model.Upload.Ago(0):0.00} MB/s", PulseSection.Energy => "80%", _ => ""
            });
            Grid.SetColumn(value, 2); row.Children.Add(value);
            var button = Button("", () => model.Navigate(section)); button.Content = row; button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.Background = active ? Brush.Parse("#008AFF") : Brushes.Transparent; button.Foreground = active ? Brushes.White : Foreground;
            if (active)
                foreach (var state in new[] { "PointerOver", "Pressed" })
                { button.Resources["ButtonBackground" + state] = Brush.Parse("#008AFF"); button.Resources["ButtonForeground" + state] = Brushes.White; }
            button.CornerRadius = new CornerRadius(7); button.Padding = new Thickness(9, 7); button.MinHeight = 33;
            Avalonia.Automation.AutomationProperties.SetName(button, Title(section)); navigation.Children.Add(button);
        }
    }
    private void BuildPage()
    {
        updates.Clear(); body.Children.Clear();
        var layout = new Grid { RowDefinitions = new RowDefinitions("76,*") };
        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(24, 18, 24, 12) };
        heading.Children.Add(Stack(Text(Title(model.Section), 21, bold: true), Text(model.Section == PulseSection.Overview ? "Your system at a glance" : "Pulse · interactive demo", 12, "#8C8C91")));
        var tools = Row();
        for (var i = 0; i < PulseModel.Ranges.Length; i++)
        {
            var range = i; var button = Button(PulseModel.Ranges[i], () => model.SetRange(range)); tools.Children.Add(button);
            updates.Add(() => button.Background = model.Range == range ? Brush.Parse("#E8E8EC") : Brushes.Transparent);
        }
        var pause = Button("Ⅱ", model.TogglePause); Avalonia.Automation.AutomationProperties.SetName(pause, "Pause updates");
        tools.Children.Add(pause); updates.Add(() => pause.Content = model.Paused ? "▶" : "Ⅱ"); Grid.SetColumn(tools, 1); heading.Children.Add(tools);
        layout.Children.Add(heading);
        Control content = model.Section switch
        {
            PulseSection.Overview => Overview(), PulseSection.Processes or PulseSection.Apps => Processes(),
            PulseSection.Connections => Connections(), PulseSection.Cleanup => Cleanup(), PulseSection.About => About(),
            _ => Hardware()
        };
        Grid.SetRow(content, 1); layout.Children.Add(content); body.Children.Add(layout);
    }
    private static string Title(PulseSection section) => section switch { PulseSection.Cpu => "CPU", PulseSection.Gpu => "GPU", PulseSection.About => "About This Demo", _ => section.ToString() };
    private static string ColorFor(PulseSection section) => section switch { PulseSection.Memory => "#2DA74D", PulseSection.Gpu => "#F24275", PulseSection.Disk => "#EF9208", PulseSection.Network => "#27A9A8", PulseSection.Energy => "#C99B00", _ => "#008AFF" };
    private static LucideIconKind Icon(PulseSection section) => section switch { PulseSection.Overview => LucideIconKind.LayoutDashboard, PulseSection.Processes => LucideIconKind.List, PulseSection.Cpu => LucideIconKind.Cpu, PulseSection.Memory => LucideIconKind.MemoryStick, PulseSection.Gpu => LucideIconKind.Monitor, PulseSection.Disk => LucideIconKind.HardDrive, PulseSection.Network or PulseSection.Connections => LucideIconKind.Network, PulseSection.Energy => LucideIconKind.Battery, PulseSection.Cleanup => LucideIconKind.Trash, PulseSection.About => LucideIconKind.Info, _ => LucideIconKind.AppWindow };
    private static TextBlock Text(string text, double size = 13, string color = "#171719", bool bold = false) => new() { Text = text, FontSize = size, Foreground = Brush.Parse(color), FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center };
    private static StackPanel Stack(params Control[] controls) { var panel = new StackPanel { Spacing = 7 }; foreach (var control in controls) panel.Children.Add(control); return panel; }
    private static StackPanel Row(params Control[] controls) { var panel = Stack(controls); panel.Orientation = Orientation.Horizontal; panel.Spacing = 6; return panel; }
    private static Button Button(string text, Action action) { var button = new Button { Content = text, HorizontalAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent, BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(7), Padding = new Thickness(10, 6), FontSize = 12 }; button.Click += (_, _) => action(); return button; }
    private static Border Card(Control child) => new() { Background = Brush.Parse("#F5F5F7"), CornerRadius = new CornerRadius(16), Padding = new Thickness(18), Child = child };
    private static ScrollViewer Scroll(Control child) => new() { Content = child, Padding = new Thickness(24, 0, 24, 20), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private void Observe(TextBlock label, Func<string> value) { updates.Add(() => label.Text = value()); label.Text = value(); }
    private PulseChart Chart(PulseSeries series, string color, double max = 100, double height = 72, PulseSeries? secondary = null)
    { var chart = new PulseChart(model, series, color, max, secondary) { Height = height, ClipToBounds = true }; updates.Add(chart.InvalidateVisual); return chart; }
    public void Dispose() { if (disposed) return; disposed = true; timer.Stop(); timer.Tick -= Tick; model.Changed -= Refresh; updates.Clear(); }
}
