namespace MySharp.Examples.Pulse;

public enum PulseSection { Overview, Processes, Connections, Cpu, Memory, Gpu, Disk, Network, Energy, Apps, Cleanup, About }
public enum ProcessSort { Name, Cpu, Gpu, Memory, Power, Threads, Time }
public sealed class PulseSeries
{
    private readonly double[] values = new double[3600];
    private int next;
    public int Version { get; private set; }
    public PulseSeries(Func<int, double> seed)
    { for (var i = -3599; i <= 0; i++) Push(seed(i)); }
    public void Push(double value) { values[next] = Math.Max(0, value); next = (next + 1) % values.Length; Version++; }
    public double Ago(int seconds) => values[(next - 1 - Math.Clamp(seconds, 0, values.Length - 1) + values.Length) % values.Length];
}
public sealed class PulseProcess(int pid, string name, string icon, string color, double cpu, double memory, double power, int threads, bool app, bool system = false)
{
    public int Pid { get; } = pid;
    public string Name { get; } = name;
    public string Icon { get; } = icon;
    public string Color { get; } = color;
    public double BaseCpu { get; } = cpu;
    public double Cpu { get; internal set; } = cpu;
    public double Memory { get; } = memory;
    public double Power { get; } = power;
    public int Threads { get; } = threads;
    public bool IsApp { get; } = app;
    public bool System { get; } = system;
    public double Gpu => Name == "WindowServer" ? 4.2 : Name == "Photos" ? 2.1 : 0;
    public string CpuTime => TimeSpan.FromSeconds(BaseCpu * 36).ToString(@"mm\:ss\.ff");
}
public sealed record PulseConnection(int Pid, string Process, string Host, string Service, string Address, string Protocol, double Down, double Up, int Latency);
public sealed class PulseModel
{
    public static readonly string[] Ranges = ["1m", "5m", "15m", "1h"];
    public static readonly int[] RangeSeconds = [60, 300, 900, 3600];
    public static readonly string[] Filters = ["All", "Apps", "My Processes", "System"];
    public PulseSection Section { get; private set; }
    public int Range { get; private set; }
    public int Filter { get; private set; }
    public bool Paused { get; private set; }
    public int Frame { get; private set; }
    public string Query { get; private set; } = "";
    public ProcessSort Sort { get; private set; } = ProcessSort.Cpu;
    public bool Descending { get; private set; } = true;
    public int? SelectedPid { get; private set; }
    public PulseProcess? SelectedProcess => Processes.FirstOrDefault(p => p.Pid == SelectedPid);
    public string Status { get; private set; } = "Demo data · no access to real processes or files";
    public List<PulseProcess> Processes { get; }
    public List<PulseConnection> Connections { get; }
    public HashSet<int> CleanupSelection { get; } = [0, 1];
    public HashSet<int> Cleaned { get; } = [];
    public static readonly (string Name, string Detail, double Size)[] CleanupItems =
    [
        ("Application caches", "Temporary files from demo applications", 1.84),
        ("Log files", "Old diagnostic logs in the sample dataset", .34),
        ("Downloads", "Example downloads kept until you select them", 2.6)
    ];
    public event Action? Changed;
    private PulseProcess[] visible = [];
    public IReadOnlyList<PulseProcess> VisibleProcesses => visible;
    public PulseSeries CpuUser { get; } = new(i => 25 + 12 * Math.Sin(i * .08) + 5 * Math.Sin(i * .39));
    public PulseSeries CpuSystem { get; } = new(i => 10 + 5 * Math.Sin(i * .11 + 2));
    public PulseSeries UsedMemory { get; } = new(i => 82 + 2 * Math.Sin(i * .012));
    public PulseSeries Gpu { get; } = new(i => 10 + 8 * Math.Sin(i * .1) + 6 * Math.Sin(i * .27));
    public PulseSeries DiskRead { get; } = new(i => 30 + 190 * Math.Pow(Math.Max(0, Math.Sin(i * .14)), 4));
    public PulseSeries DiskWrite { get; } = new(i => 15 + 95 * Math.Pow(Math.Max(0, Math.Sin(i * .16 + 2)), 3));
    public PulseSeries Download { get; } = new(i => .3 + 2.3 * Math.Pow(Math.Max(0, Math.Sin(i * .15 + 1)), 3));
    public PulseSeries Upload { get; } = new(i => .02 + .1 * Math.Max(0, Math.Sin(i * .11)));
    public PulseSeries Battery { get; } = new(_ => 80);
    public double Cpu => Math.Round(CpuUser.Ago(0) + CpuSystem.Ago(0));
    public double MemoryGb => UsedMemory.Ago(0) * 32 / 100;
    public PulseModel()
    {
        Processes =
        [
            new(401,"Xcode","Hammer","#168AFF",42,3482,2.6,64,true),
            new(413,"node","Hexagon","#579A48",21,642,.9,11,false),
            new(437,"Safari","Compass","#168AFF",18,1823,1.1,41,true),
            new(456,"python3.12","Code","#367AAA",9.4,288,.38,14,false),
            new(467,"Photos","Image","#DE5C7B",9,1137,.42,28,true),
            new(89,"WindowServer","Settings","#92949C",8.1,512,.31,24,false,true),
            new(0,"kernel_task","Settings","#92949C",6.4,40,.22,520,false,true),
            new(134,"mds_stores","Settings","#92949C",3.1,98,.09,7,false,true),
            new(521,"Music","Music","#EB3E65",3,386,.18,22,true),
            new(540,"Mail","Mail","#168AFF",1.2,418,.05,19,true),
            new(153,"coreaudiod","Settings","#92949C",1.2,22,.03,9,false,true),
            new(302,"Finder","Folder","#168AFF",1.1,262,.04,9,true),
            new(561,"Messages","MessageCircle","#35BF65",.6,204,.03,12,true),
            new(584,"git","GitBranch","#F15B45",.6,12,.02,1,false),
            new(603,"Maps","Map","#23AA94",.4,233,.02,11,true),
            new(172,"bluetoothd","Bluetooth","#92949C",.4,18,0,6,false,true),
            new(622,"Notes","Notebook","#E8B839",.3,148,.01,8,true),
            new(641,"Calendar","Calendar","#EF5B67",.2,121,.01,7,true),
            new(1,"launchd","Settings","#92949C",.2,31,0,4,false,true),
            new(190,"cfprefsd","Settings","#92949C",.2,8,0,4,false,true),
            new(662,"Freeform","PenTool","#25A6AA",.1,132,0,6,true),
            new(212,"trustd","ShieldCheck","#92949C",.1,14,0,3,false,true),
            new(684,"Preview","Image","#168AFF",0,96,0,5,true),
            new(702,"zsh","Terminal","#F3913D",0,6,0,1,false)
        ];
        Connections =
        [
            new(437,"Safari","developer.apple.com","HTTPS","17.253.144.10:443","TCP",840,12,24),
            new(413,"node","registry.npmjs.org","HTTPS","104.16.26.34:443","TCP",420,8,18),
            new(401,"Xcode","github.com","HTTPS","140.82.114.3:443","TCP",185,6,32),
            new(540,"Mail","imap.example.com","IMAPS","192.0.2.24:993","TCP",12,3,44),
            new(641,"Calendar","calendar.example.com","HTTPS","192.0.2.42:443","TCP",4,.3,37),
            new(702,"zsh","localhost","SSH","127.0.0.1:22","TCP",0,0,1)
        ];
        CpuUser.Push(37); CpuSystem.Push(22); UsedMemory.Push(26.9 / 32 * 100); Gpu.Push(8.1);
        DiskRead.Push(200); DiskWrite.Push(110); Download.Push(2.39); Upload.Push(.0137);
        RefreshRows();
    }
    public void Navigate(PulseSection section) { Section = section; Query = ""; RefreshRows(); Notify(); }
    public void SetRange(int range) { if (range is < 0 or > 3 || Range == range) return; Range = range; Notify(); }
    public void SetFilter(int filter) { if (filter is < 0 or > 3) return; Filter = filter; RefreshRows(); Notify(); }
    public void SetQuery(string query) { Query = query; RefreshRows(); Notify(); }
    public void TogglePause() { Paused = !Paused; Notify(); }
    public void SetSort(ProcessSort sort)
    {
        Descending = Sort == sort ? !Descending : sort != ProcessSort.Name;
        Sort = sort; RefreshRows(); Notify();
    }
    public void Select(int? pid) { SelectedPid = pid; Notify(); }
    public void ToggleInspector() { Select(SelectedPid.HasValue ? null : visible.FirstOrDefault()?.Pid); }
    public void QuitSelected(bool force)
    {
        if (SelectedProcess is not {} process) return;
        Processes.Remove(process); Connections.RemoveAll(c => c.Pid == process.Pid);
        SelectedPid = null; Status = $"Demo: {process.Name} {(force ? "force quit" : "quit")} · no real process was affected";
        RefreshRows(); Notify();
    }
    public void Clean()
    {
        foreach (var i in CleanupSelection) Cleaned.Add(i);
        CleanupSelection.Clear(); Status = "Demo cleanup complete · no real file was removed"; Notify();
    }
    public void Tick()
    {
        if (Paused) return;
        Frame++;
        CpuUser.Push(32 + 10 * Math.Sin(Frame * .15)); CpuSystem.Push(16 + 6 * Math.Sin(Frame * .2 + 1));
        UsedMemory.Push(84 + 1.5 * Math.Sin(Frame * .025)); Gpu.Push(8 + 7 * Math.Sin(Frame * .18));
        DiskRead.Push(40 + 190 * Math.Pow(Math.Max(0, Math.Sin(Frame * .14)), 4));
        DiskWrite.Push(20 + 90 * Math.Pow(Math.Max(0, Math.Sin(Frame * .16 + 2)), 3));
        Download.Push(.3 + 2.2 * Math.Pow(Math.Max(0, Math.Sin(Frame * .15 + 1)), 3));
        Upload.Push(.02 + .1 * Math.Max(0, Math.Sin(Frame * .11)));
        foreach (var p in Processes) p.Cpu = Math.Max(0, p.BaseCpu * (1 + .06 * Math.Sin(Frame * .2 + p.Pid)));
        RefreshRows(); Notify();
    }
    private void RefreshRows()
    {
        var query = Query.Trim();
        IEnumerable<PulseProcess> rows = Processes.Where(p => (query.Length == 0 || p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || p.Pid.ToString().Contains(query)) && (Filter switch
        {
            1 => p.IsApp, 2 => !p.System, 3 => p.System, _ => true
        }));
        Func<PulseProcess, object> key = Sort switch
        {
            ProcessSort.Name => p => p.Name, ProcessSort.Memory => p => p.Memory, ProcessSort.Gpu => p => p.Gpu,
            ProcessSort.Power => p => p.Power, ProcessSort.Threads => p => p.Threads,
            ProcessSort.Time => p => p.BaseCpu, _ => p => p.Cpu
        };
        var next = (Descending ? rows.OrderByDescending(key) : rows.OrderBy(key)).ThenBy(p => p.Pid).ToArray();
        if (!visible.SequenceEqual(next)) visible = next;
        if (SelectedPid.HasValue && !Processes.Any(p => p.Pid == SelectedPid)) SelectedPid = null;
    }
    private void Notify() => Changed?.Invoke();
}
