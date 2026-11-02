using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MySharp.Examples.Pulse;

public sealed partial class PulseView
{
    private (PulseSeries Series, double Max, Func<string> Value, string Detail, string Stats) Metric(PulseSection section) => section switch
    {
        PulseSection.Cpu => (model.CpuUser, 100, () => $"{model.Cpu:0}%", "Total utilization", "User 37%      System 22%      Idle 41%"),
        PulseSection.Memory => (model.UsedMemory, 100, () => $"{model.MemoryGb:0.0} GB", "of 32 GB physical memory", "App 11.3 GB      Wired 4.48 GB"),
        PulseSection.Gpu => (model.Gpu, 100, () => $"{model.Gpu.Ago(0):0.0}%", "GPU utilization", "Apple M5      10 cores      38 °C"),
        PulseSection.Disk => (model.DiskRead, 500, () => $"{model.DiskRead.Ago(0) + model.DiskWrite.Ago(0):0} KB/s", "Combined disk activity", "Read 200 KB/s      Write 110 KB/s"),
        PulseSection.Network => (model.Download, 4, () => $"{model.Download.Ago(0) + model.Upload.Ago(0):0.00} MB/s", "Combined network activity", "↓ Download 2.39 MB/s      ↑ 14 KB/s"),
        _ => (model.Battery, 100, () => "80%", "Battery · connected to power", "Health 100%      Cycles 42      28 °C")
    };
    private Control Overview()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto"), ColumnSpacing = 14, RowSpacing = 14 };
        var sections = new[] { PulseSection.Cpu, PulseSection.Memory, PulseSection.Gpu, PulseSection.Disk, PulseSection.Network, PulseSection.Energy };
        for (var i = 0; i < sections.Length; i++)
        {
            var section = sections[i]; var metric = Metric(section); var color = ColorFor(section);
            var value = Text("", section == PulseSection.Network ? 30 : 35, bold: true); Observe(value, metric.Value);
            var icon = new Lucide.Avalonia.LucideIcon { Kind = Icon(section), Size = 17, Foreground = Brush.Parse(color) };
            var chart = Chart(metric.Series, color, metric.Max, 64, section == PulseSection.Cpu ? model.CpuSystem : null);
            var card = Card(Stack(Row(icon, Text(Title(section), 13, bold: true)), value, Text(metric.Detail, 11, "#8C8C91"), chart,
                Button("View details →", () => model.Navigate(section))));
            Grid.SetColumn(card, i % 3); Grid.SetRow(card, i / 3); grid.Children.Add(card);
        }
        var ring = new Grid { Height = 112 };
        ring.Children.Add(new PulseMemoryRing());
        var used = Text("84%", 25, bold: true); used.HorizontalAlignment = HorizontalAlignment.Center; ring.Children.Add(used);
        var memory = Card(Stack(Text("Memory by type", 14, bold: true), ring, Text("App · Wired · Compressed", 11, "#8C8C91"), Text("Normal memory pressure", 11, "#2DA74D")));
        Grid.SetRow(memory, 2); grid.Children.Add(memory);
        var treemap = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,2*"), RowDefinitions = new RowDefinitions("*,*"), Height = 133, ColumnSpacing = 3, RowSpacing = 3 };
        var appNames = new[] { "Xcode\n3.48 GB", "Safari\n1.82 GB", "Photos\n1.14 GB", "Others\n2.30 GB" };
        var appColors = new[] { "#008AFF", "#27A9A8", "#F24275", "#2DA74D" };
        for (var i = 0; i < 4; i++) { var label = Text(appNames[i], 12, "#FFFFFF"); label.TextWrapping = TextWrapping.Wrap; var tile = new Border { Background = Brush.Parse(appColors[i]), CornerRadius = new CornerRadius(5), Padding = new Thickness(9), Child = label }; Grid.SetColumn(tile, i % 2); Grid.SetRow(tile, i / 2); treemap.Children.Add(tile); }
        var apps = Card(Stack(Text("Memory by app", 14, bold: true), treemap, Text("Resident memory · sample", 11, "#8C8C91")));
        Grid.SetRow(apps, 2); Grid.SetColumn(apps, 1); grid.Children.Add(apps);
        var energy = Card(Stack(Text("Energy by app", 14, bold: true), AppBar("Xcode", "2.6 W", .86, "#EF9208"), AppBar("Safari", "1.1 W", .4, "#EF9208"), AppBar("node", "0.9 W", .32, "#EF9208"), Text("Last 60 seconds · demo", 11, "#8C8C91")));
        Grid.SetRow(energy, 2); Grid.SetColumn(energy, 2); grid.Children.Add(energy);
        return Scroll(grid);
    }
    private static Control AppBar(string name, string value, double fraction, string color)
    {
        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        heading.Children.Add(Text(name, 12)); var number = Text(value, 11, "#8C8C91"); Grid.SetColumn(number, 1); heading.Children.Add(number);
        return Stack(heading, new ProgressBar { Minimum = 0, Maximum = 1, Value = fraction, Height = 5, Foreground = Brush.Parse(color), Background = Brush.Parse("#E6E6EA") });
    }
    private static Control MemoryBar()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("11.3*,4.48*,11.12*,4.03*,1.07*"), Height = 30, Margin = new Thickness(0, 12) };
        var colors = new[] { "#2DA74D", "#EF9208", "#F24275", "#008AFF", "#DCDCE1" };
        for (var i = 0; i < colors.Length; i++) { var block = new Border { Background = Brush.Parse(colors[i]), Margin = new Thickness(1) }; Grid.SetColumn(block, i); grid.Children.Add(block); }
        return grid;
    }
    private Control Hardware()
    {
        var metric = Metric(model.Section); var color = ColorFor(model.Section);
        var number = Text("", 42, bold: true); Observe(number, metric.Value);
        var statistics = Text(metric.Stats, 12);
        if (model.Section == PulseSection.Cpu) Observe(statistics, () => $"User {model.CpuUser.Ago(0):0}%      System {model.CpuSystem.Ago(0):0}%      Idle {100 - model.Cpu:0}%");
        if (model.Section == PulseSection.Disk) Observe(statistics, () => $"Read {model.DiskRead.Ago(0):0} KB/s      Write {model.DiskWrite.Ago(0):0} KB/s");
        if (model.Section == PulseSection.Network) Observe(statistics, () => $"↓ {model.Download.Ago(0):0.00} MB/s      ↑ {model.Upload.Ago(0) * 1024:0} KB/s");
        var main = Card(Stack(Text(Title(model.Section) + " history", 14, bold: true), number, Text(metric.Detail, 12, "#8C8C91"),
            Chart(metric.Series, color, metric.Max, 184, model.Section == PulseSection.Cpu ? model.CpuSystem : null), statistics));
        var column = Stack(main); column.Spacing = 16;
        if (model.Section == PulseSection.Cpu)
        {
            var cores = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 24 };
            var super = Stack(Text("Super cores", 14, bold: true)); var efficiency = Stack(Text("Efficiency cores", 14, bold: true));
            for (var i = 0; i < 10; i++) { var core = i; var bar = new ProgressBar { Maximum = 100, Height = 8, Foreground = Brush.Parse(i < 4 ? "#008AFF" : "#27A9A8") }; updates.Add(() => bar.Value = 25 + 20 * (1 + Math.Sin(model.Frame * .13 + core))); var row = Stack(Text($"Core {i + 1}", 12, "#8C8C91"), bar); (i < 4 ? super : efficiency).Children.Add(row); }
            cores.Children.Add(super); Grid.SetColumn(efficiency, 1); cores.Children.Add(efficiency); column.Children.Add(Card(cores));
        }
        else if (model.Section == PulseSection.Memory) column.Children.Add(Card(Stack(Text("Memory allocation", 16, bold: true), MemoryBar(), Text("App 11.3 GB · Wired 4.48 GB · Compressed 11.12 GB", 13), Text("Swap used 0 bytes · pressure normal", 12, "#8C8C91"))));
        else column.Children.Add(Card(Stack(Text("Device information", 16, bold: true), Text(metric.Stats), Text("Illustrative readings from a sample workstation. Not your device's telemetry.", 12, "#8C8C91"))));
        column.Children.Add(Card(Stack(Text("Top applications", 14, bold: true), AppBar("Xcode", "42.0% CPU", .84, color), AppBar("node", "21.0% CPU", .42, color), AppBar("Safari", "18.0% CPU", .36, color))));
        return Scroll(column);
    }
    private Control Connections()
    {
        var column = Stack(Text("Active connections · sample data", 14, bold: true));
        var search = new TextBox { PlaceholderText = "Search host or process", Margin = new Thickness(0, 0, 0, 12) };
        var rows = new StackPanel { Spacing = 8 };
        void Update() { rows.Children.Clear(); foreach (var c in model.Connections.Where(c => (c.Process + c.Host).Contains(search.Text ?? "", StringComparison.OrdinalIgnoreCase))) rows.Children.Add(Card(Stack(Text(c.Process + "  →  " + c.Host, 14, bold: true), Text($"{c.Protocol} · {c.Service} · {c.Address} · {c.Latency} ms", 12, "#8C8C91")))); }
        search.TextChanged += (_, _) => Update(); Update(); column.Children.Add(search); column.Children.Add(rows); return Scroll(column);
    }
    private Control Cleanup()
    {
        var column = Stack(Text("Free up space", 27, bold: true), Text("Demo only. These controls never inspect or delete your files.", 13, "#8C8C91"));
        var checks = new List<CheckBox>();
        for (var i = 0; i < PulseModel.CleanupItems.Length; i++)
        {
            var index = i; var item = PulseModel.CleanupItems[i]; var check = new CheckBox { Content = Stack(Text($"{item.Name} · {item.Size:0.00} GB", 15, bold: true), Text(item.Detail, 12, "#8C8C91")), IsChecked = model.CleanupSelection.Contains(index), Padding = new Thickness(8) };
            check.IsCheckedChanged += (_, _) => { if (check.IsChecked == true) model.CleanupSelection.Add(index); else model.CleanupSelection.Remove(index); };
            updates.Add(() => { check.IsEnabled = !model.Cleaned.Contains(index); if (model.Cleaned.Contains(index)) check.IsChecked = false; });
            checks.Add(check); column.Children.Add(Card(check));
        }
        column.Children.Add(Button("Simulate cleanup", model.Clean)); return Scroll(column);
    }
    private static Control About() => Scroll(Card(Stack(Text("Pulse", 40, bold: true), Text("System monitor · MySharp Gallery", 17), Text("A native, code-only recreation inspired by pulse.egoist.dev.", 13), Text("All readings, process names and device information are sample data.", 13, "#8C8C91"), Text("Built with C#, Avalonia and Lucide icons. No web view or XAML.", 13))));
}
