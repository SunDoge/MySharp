using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using MySharp;
using MySharp.Examples.Pulse;

internal static class PulseTests
{
    internal static int Run()
    {
        var count = 0;
        void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
        void Test(string name, Action action) { action(); count++; Console.WriteLine("PASS Pulse " + name); }
        Test("history wraps and clamps range", () =>
        {
            var series = new PulseSeries(i => i + 3599);
            Check(series.Ago(0) == 3599 && series.Ago(3599) == 0, "Seed order");
            series.Push(4200); Check(series.Ago(0) == 4200 && series.Ago(36000) == 1, "Ring wrap");
        });
        Test("search filters sorting and selection use process identity", () =>
        {
            var m = new PulseModel(); m.SetQuery("SAFARI"); Check(m.VisibleProcesses.Single().Name == "Safari", "Case insensitive query");
            m.SetQuery("401"); Check(m.VisibleProcesses.Single().Name == "Xcode", "PID query");
            m.Select(401); m.SetQuery(""); m.SetSort(ProcessSort.Memory); m.Tick(); Check(m.SelectedPid == 401, "Selection survives sort and tick");
            Check(m.VisibleProcesses[0].Name == "Xcode", "Descending memory sort");
            m.SetFilter(3); Check(m.VisibleProcesses.All(p => p.System), "System filter");
            m.SetFilter(1); Check(m.VisibleProcesses.All(p => p.IsApp), "App filter");
        });
        Test("pause freezes all demo readings and invalid ranges are ignored", () =>
        {
            var m = new PulseModel(); var version = m.CpuUser.Version; m.TogglePause(); m.Tick();
            Check(m.Frame == 0 && m.CpuUser.Version == version, "No work while paused");
            m.TogglePause(); m.Tick(); Check(m.Frame == 1 && m.CpuUser.Version == version + 1, "Resume");
            m.SetRange(3); m.SetRange(9); Check(m.Range == 3, "Invalid range");
        });
        Test("quit and cleanup only mutate sample records", () =>
        {
            var m = new PulseModel(); m.Select(401); m.QuitSelected(true);
            Check(!m.Processes.Any(p => p.Pid == 401) && !m.Connections.Any(c => c.Pid == 401) && m.SelectedPid is null, "Sample process and connection removed");
            m.Clean(); Check(m.Cleaned.SetEquals([0, 1]) && m.CleanupSelection.Count == 0 && m.Status.Contains("no real file"), "Sample cleanup only");
        });
        Test("retained search caret rows selection and timer lifecycle", () =>
        {
            var model = new PulseModel(); using var view = new PulseView(model, () => { });
            var window = new Window { Content = view, Width = 1120, Height = 820 }; window.Show(); Dispatcher.UIThread.RunJobs();
            try
            {
                Check(view.TimerRunning, "Attached timer");
                model.Navigate(PulseSection.Processes); Dispatcher.UIThread.RunJobs();
                var search = view.GetLogicalDescendants().OfType<TextBox>().Single(); search.Text = "Safari"; search.CaretIndex = 3;
                model.Tick(); Dispatcher.UIThread.RunJobs();
                Check(ReferenceEquals(search, view.GetLogicalDescendants().OfType<TextBox>().Single()) && search.CaretIndex == 3, "Retained editor and caret");
                var row = view.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Safari process");
                row.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs(); Check(model.SelectedProcess?.Name == "Safari", "Native click selection");
                model.TogglePause(); Check(!view.TimerRunning, "Pause stops timer"); model.TogglePause(); Check(view.TimerRunning, "Resume starts timer");
                window.Content = null; Dispatcher.UIThread.RunJobs(); Check(!view.TimerRunning, "Detached timer");
            }
            finally { window.Close(); }
        });
        Test("Gallery can enter Pulse and return", () =>
        {
            using var workspace = new Workspace("pulse"); using var root = new UiView(workspace.Build);
            var window = new Window { Content = root, Width = 960, Height = 680 }; window.Show(); Dispatcher.UIThread.RunJobs();
            try
            {
                Check(root.GetLogicalDescendants().OfType<PulseView>().Any(), "Pulse entry");
                root.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "← Back to Gallery")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs(); Check(!root.GetLogicalDescendants().OfType<PulseView>().Any(), "Back returns to Gallery");
            }
            finally { window.Close(); }
        });
        return count;
    }
}
