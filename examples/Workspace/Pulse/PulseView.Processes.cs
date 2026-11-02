using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MySharp.Examples.Pulse;

public sealed partial class PulseView
{
    private Control Processes()
    {
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), Margin = new Thickness(24, 0, 24, 10) };
        var search = new TextBox { PlaceholderText = "Search processes", Text = model.Query, MinWidth = 170 };
        Avalonia.Automation.AutomationProperties.SetName(search, "Search processes");
        search.TextChanged += (_, _) => { if (model.Query != (search.Text ?? "")) model.SetQuery(search.Text ?? ""); };
        var toolbar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        toolbar.Children.Add(search);
        var filters = Row();
        for (var i = 0; i < PulseModel.Filters.Length; i++)
        {
            var index = i; var button = Button(PulseModel.Filters[i], () => model.SetFilter(index));
            filters.Children.Add(button); updates.Add(() => button.Background = model.Filter == index ? Brush.Parse("#E8E8EC") : Brushes.Transparent);
        }
        Grid.SetColumn(filters, 1); toolbar.Children.Add(filters); layout.Children.Add(toolbar);
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,76,88,74,66"), Margin = new Thickness(0, 12, 0, 5) };
        var headers = new[] { ("Name", ProcessSort.Name), ("% CPU", ProcessSort.Cpu), ("Memory", ProcessSort.Memory), ("Power", ProcessSort.Power), ("Threads", ProcessSort.Threads) };
        for (var i = 0; i < headers.Length; i++)
        {
            var (name, sort) = headers[i]; var button = Button(name, () => model.SetSort(sort)); button.HorizontalAlignment = i == 0 ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
            updates.Add(() => button.Content = name + (model.Sort == sort ? model.Descending ? " ↓" : " ↑" : "")); Grid.SetColumn(button, i); header.Children.Add(button);
        }
        Grid.SetRow(header, 1); layout.Children.Add(header);
        var area = new Grid { ColumnDefinitions = new ColumnDefinitions("*,0") };
        var rows = new StackPanel { Spacing = 0 };
        var scroll = new ScrollViewer { Content = rows, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        area.Children.Add(scroll);
        var inspector = new Border { Padding = new Thickness(16, 0, 0, 0), BorderThickness = new Thickness(1, 0, 0, 0), BorderBrush = Brush.Parse("#E4E4E7") }; Grid.SetColumn(inspector, 1); area.Children.Add(inspector);
        Grid.SetRow(area, 2); layout.Children.Add(area);
        PulseProcess[] previous = []; int? selected = null; Action? inspectorUpdate = null;
        var cells = new List<(PulseProcess Process, Button Button, TextBlock[] Texts)>();
        void Update()
        {
            var source = model.VisibleProcesses.Where(p => model.Section != PulseSection.Apps || p.IsApp).ToArray();
            if (!source.SequenceEqual(previous))
            {
                previous = source; rows.Children.Clear(); cells.Clear();
                foreach (var process in source)
                {
                    var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,76,88,74,66") };
                    var texts = new[] { Text(process.Name, 12), Text("", 12), Text("", 12), Text("", 12), Text("", 12) };
                    var name = new Grid { ColumnDefinitions = new ColumnDefinitions("23,*") };
                    var kind = Enum.TryParse<Lucide.Avalonia.LucideIconKind>(process.Icon, out var parsed) ? parsed : Lucide.Avalonia.LucideIconKind.AppWindow;
                    name.Children.Add(new Lucide.Avalonia.LucideIcon { Kind = kind, Size = 15, Foreground = Brush.Parse(process.Color) });
                    Grid.SetColumn(texts[0], 1); name.Children.Add(texts[0]); grid.Children.Add(name);
                    for (var j = 1; j < texts.Length; j++) { texts[j].HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumn(texts[j], j); grid.Children.Add(texts[j]); }
                    var button = Button("", () => model.Select(process.Pid)); button.Content = grid; button.Padding = new Thickness(8, 7); button.MinHeight = 32; button.CornerRadius = new CornerRadius(4); button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                    Avalonia.Automation.AutomationProperties.SetName(button, process.Name + " process"); rows.Children.Add(button); cells.Add((process, button, texts));
                }
                if (source.Length == 0) rows.Children.Add(Text("No matching processes", 13, "#8C8C91"));
            }
            var index = 0;
            foreach (var (process, button, texts) in cells)
            {
                var active = model.SelectedPid == process.Pid;
                button.Background = Brush.Parse(active ? "#008AFF" : index++ % 2 == 0 ? "#F5F5F7" : "#FFFFFF");
                foreach (var state in new[] { "PointerOver", "Pressed" }) button.Resources["ButtonBackground" + state] = Brush.Parse(active ? "#008AFF" : "#E8E8EC");
                foreach (var text in texts) text.Foreground = active ? Brushes.White : Brush.Parse("#343438");
                texts[1].Text = process.Cpu.ToString("0.0"); texts[2].Text = process.Memory >= 1024 ? $"{process.Memory / 1024:0.00} GB" : $"{process.Memory:0} MB";
                texts[3].Text = $"{process.Power:0.00} W"; texts[4].Text = process.Threads.ToString();
            }
            if (selected != model.SelectedPid)
            {
                selected = model.SelectedPid;
                area.ColumnDefinitions[1].Width = new GridLength(selected.HasValue ? 224 : 0);
                header.Margin = new Thickness(0, 12, selected.HasValue ? 224 : 0, 5);
                inspectorUpdate = null;
                inspector.Child = model.SelectedProcess is {} process ? Inspector(process, out inspectorUpdate) : null;
            }
            inspectorUpdate?.Invoke();
        }
        updates.Add(Update); Update(); return layout;
    }
    private Control Inspector(PulseProcess process, out Action update)
    {
        var cpu = new PulseSeries(i => process.BaseCpu * (.9 + .1 * Math.Sin(i * .17)));
        var memory = new PulseSeries(_ => process.Memory);
        var cpuChart = new PulseChart(model, cpu, "#008AFF") { Height = 80 };
        var memoryChart = new PulseChart(model, memory, "#2DA74D", Math.Max(1024, process.Memory * 1.2)) { Height = 80 };
        var utilization = Text("", 12); var frame = model.Frame;
        update = () =>
        {
            if (frame != model.Frame) { cpu.Push(process.Cpu); memory.Push(process.Memory); frame = model.Frame; }
            utilization.Text = $"{process.Cpu:0.0}% utilization";
            cpuChart.InvalidateVisual(); memoryChart.InvalidateVisual();
        };
        update();
        return new ScrollViewer { Content = Stack(Button("Close inspector ×", () => model.Select(null)), Text(process.Name, 22, bold: true), Text($"PID {process.Pid} · demo user", 12, "#8C8C91"),
            Row(Button("Quit", () => model.QuitSelected(false)), Button("Force Quit", () => model.QuitSelected(true))),
            Text("Demo actions only", 11, "#8C8C91"), Text("CPU", 13, bold: true), cpuChart, utilization,
            Text("Memory", 13, bold: true), memoryChart, Text($"{process.Memory:0} MB resident", 12),
            Text($"Threads       {process.Threads}\n\nGPU             {process.Gpu:0.0}%\n\nCPU time      {process.CpuTime}\n\nPower          {process.Power:0.00} W", 12)) };
    }
}
