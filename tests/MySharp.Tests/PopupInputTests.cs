using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using MySharp;

internal static class PopupInputTests
{
    internal static int Run()
    {
        var passed = 0;
        void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        void Test(string name, Action test) { test(); passed++; Console.WriteLine($"PASS {name}"); }
        void Pump(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
        Test("owned flyout content starts lazily and releases on hide and scope removal", () =>
        {
            var mounted = true;
            var prefix = "first";
            var builds = 0;
            var released = 0;
            using var shared = State.Create(0);
            UiFlyout? flyout = null;
            Button? button = null;
            using var view = new UiView(cx =>
            {
                var element = Ui.Button(cx, "Open");
                button = element.Native<Button>();
                if (!mounted) return;
                var snapshot = prefix;
                flyout = cx.Flyout(body =>
                {
                    builds++; _ = shared.Value;
                    body.Resource(() => new Cleanup(() => released++));
                    Ui.Text(body, snapshot);
                });
                element.Flyout(flyout);
            });
            var first = flyout;
            var window = new Window { Width = 400, Height = 300, Content = view };
            window.Styles.Add(new FluentTheme());
            try
            {
                window.Show(); Pump(window);
                Assert(builds == 0, "Hidden content must not create resources.");
                flyout!.ShowAt(button!); Pump(window);
                Assert(flyout.Native.IsOpen && builds > 0, "Visible flyout must build content.");
                prefix = "second"; view.Rebuild(); Pump(window);
                Assert(ReferenceEquals(first, flyout), "Flyout identity must retain.");
                var content = (Control)flyout.Native.Content!;
                Assert(content.GetLogicalDescendants().OfType<TextBlock>().Single().Text == "second", "Visible flyout gets latest captures.");
                flyout.Hide(); Pump(window);
                Assert(!flyout.Native.IsOpen && released == 1, "Hide releases owned content.");
                var before = builds;
                shared.Value++; Pump(window);
                Assert(builds == before, "Hidden content must unsubscribe.");
                flyout.ShowAt(button!); Pump(window);
                mounted = false; view.Rebuild(); Pump(window);
                Assert(released == 2 && !flyout.Native.IsOpen && button!.Flyout is null, "Scope removal closes and disposes the flyout.");
            }
            finally { window.Close(); }
        });
        Test("native context menus close on declaration omission and unmount", () =>
        {
            var attached = true;
            var mounted = true;
            var menu = new ContextMenu();
            menu.Items.Add(new MenuItem { Header = "Action" });
            Button? button = null;
            using var view = new UiView(cx =>
            {
                if (!mounted) return;
                var element = Ui.Button(cx, "Context"); button = element.Native<Button>();
                if (attached) element.ContextMenu(menu);
            });
            var window = new Window { Width = 400, Height = 300, Content = view };
            window.Styles.Add(new FluentTheme());
            try
            {
                window.Show(); Pump(window);
                menu.Open(button!); Pump(window);
                Assert(menu.IsOpen, "Native context menu must open.");
                attached = false; view.Rebuild(); Pump(window);
                Assert(!menu.IsOpen && button!.ContextMenu is null, "Omitted menu closes and clears property.");
                attached = true; view.Rebuild(); menu.Open(button!); Pump(window);
                mounted = false; view.Rebuild(); Pump(window);
                Assert(!menu.IsOpen, "Unmount closes the menu.");
            }
            finally { menu.Close(); window.Close(); }
        });
        Test("button and context flyouts close on replacement omission and unmount", () =>
        {
            foreach (var contextFlyout in new[] { false, true })
            {
                var first = new Flyout { Content = new TextBlock { Text = "first" } };
                var second = new Flyout { Content = new TextBlock { Text = "second" } };
                Flyout current = first;
                var declared = true;
                var mounted = true;
                Button? button = null;
                using var view = new UiView(cx =>
                {
                    if (!mounted) return;
                    var element = Ui.Button(cx, "Target"); button = element.Native<Button>();
                    if (!declared) return;
                    if (contextFlyout) element.ContextFlyout(current);
                    else element.Flyout(current);
                });
                var window = new Window { Width = 400, Height = 300, Content = view };
                window.Styles.Add(new FluentTheme());
                try
                {
                    window.Show(); Pump(window); first.ShowAt(button!); Pump(window);
                    Assert(first.IsOpen, "Initial flyout opens.");
                    current = second; view.Rebuild(); Pump(window);
                    Assert(!first.IsOpen, "Replacing the attachment closes its old flyout.");
                    second.ShowAt(button!); Pump(window);
                    declared = false; view.Rebuild(); Pump(window);
                    Assert(!second.IsOpen && (contextFlyout ? button!.ContextFlyout is null : button!.Flyout is null), "Omitted flyout closes and clears property.");
                    declared = true; view.Rebuild(); second.ShowAt(button!); Pump(window);
                    mounted = false; view.Rebuild(); Pump(window);
                    Assert(!second.IsOpen, "Unmount closes attached flyout.");
                }
                finally { first.Hide(); second.Hide(); window.Close(); }
            }
        });
        Test("input modifiers and hotkeys reset when omitted", () =>
        {
            var declarations = true;
            Button? button = null;
            using var view = new UiView(cx =>
            {
                var element = Ui.Button(cx, "Key"); button = element.Native<Button>();
                if (declarations) element.Focusable(false).TabIndex(8).TabStop(false).AutomationId("key-button")
                    .HotKey(new KeyGesture(Key.K, KeyModifiers.Control));
            });
            Assert(!button!.Focusable && button.TabIndex == 8 && !button.IsTabStop && button.HotKey?.Key == Key.K,
                "Native input modifiers must apply.");
            Assert(Avalonia.Automation.AutomationProperties.GetAutomationId(button) == "key-button", "Automation ID applies.");
            declarations = false; view.Rebuild();
            var defaults = new Button();
            Assert(button.Focusable == defaults.Focusable && button.TabIndex == defaults.TabIndex && button.IsTabStop == defaults.IsTabStop && button.HotKey is null,
                "Omitted modifiers restore native defaults.");
            Assert(Avalonia.Automation.AutomationProperties.GetAutomationId(button) is null, "Automation ID clears.");
        });
        Test("routed handlers update routing and handled-event options without duplication", () =>
        {
            var enabled = true;
            var handledToo = false;
            var calls = 0;
            Button? button = null;
            using var view = new UiView(cx =>
            {
                var element = Ui.Button(cx, "Event"); button = element.Native<Button>();
                if (enabled) element.On(InputElement.KeyDownEvent, (_, _) => calls++, RoutingStrategies.Tunnel, handledToo);
            });
            void Raise(bool handled) => button!.RaiseEvent(new KeyEventArgs
            { RoutedEvent = InputElement.KeyDownEvent, Key = Key.K, Handled = handled });
            Raise(true);
            Assert(calls == 0, "Handled events are skipped by default.");
            handledToo = true; view.Rebuild(); view.Rebuild();
            Raise(true);
            Assert(calls == 1, "Updated registration receives handled event once.");
            enabled = false; view.Rebuild();
            Raise(false);
            Assert(calls == 1, "Omitted registration detaches.");
            Dispatcher.UIThread.RunJobs();
        });
        Test("headless keyboard dispatch activates hotkeys and follows tab order", () =>
        {
            var calls = 0;
            var bound = true;
            var disabled = false;
            Button? button = null;
            TextBox? first = null;
            TextBox? second = null;
            using var view = new UiView(cx =>
            {
                Ui.Column(cx).Children(() =>
                {
                    first = Ui.TextInput(cx, "first", _ => { }).TabIndex(1).TabStop().Native<TextBox>();
                    second = Ui.TextInput(cx, "second", _ => { }).TabIndex(2).TabStop().Native<TextBox>();
                    var element = Ui.Button(cx, "Run").TabIndex(3).TabStop().Disabled(disabled).OnClick(() => calls++);
                    if (bound) element.HotKey(new KeyGesture(Key.K, KeyModifiers.Control));
                    button = element.Native<Button>();
                });
            });
            var window = new Window { Width = 400, Height = 300, Content = view };
            window.Styles.Add(new FluentTheme());
            try
            {
                window.Show(); Pump(window); first!.Focus();
                window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None); window.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.None); Pump(window);
                Assert(second!.IsFocused, "Tab uses declared order through native input dispatch.");
                window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.Control); window.KeyReleaseQwerty(PhysicalKey.K, RawInputModifiers.Control); Pump(window);
                Assert(calls == 1, "Hotkey must invoke the real button click.");
                disabled = true; view.Rebuild();
                window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.Control); window.KeyReleaseQwerty(PhysicalKey.K, RawInputModifiers.Control); Pump(window);
                Assert(calls == 1, "Disabled button must not invoke a hotkey.");
                disabled = false; bound = false; view.Rebuild();
                window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.Control); window.KeyReleaseQwerty(PhysicalKey.K, RawInputModifiers.Control); Pump(window);
                Assert(calls == 1 && button!.HotKey is null, "Omitted hotkey unregisters native dispatch.");
            }
            finally { window.Close(); }
        });
        return passed;
    }
}
