using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using MySharp;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--typography") { TypographySmoke.Run(args[1]); return; }
        UiContext? primary = null;
        var picker = args.Contains("--picker");
        Console.WriteLine($"Native smoke start; picker={picker}");
        var report = new Dictionary<string, object> { ["picker_requested"] = picker };
        MySharpApp.Run(cx => { primary = cx; Ui.Text(cx, "Native service smoke"); }, new WindowOptions
        {
            Title = "MySharp Native Smoke", Width = 480, Height = 240, MinWidth = 320, MinHeight = 200,
            Opened = async window =>
            {
                var temporary = Path.Combine(Path.GetTempPath(), "mysharp-native-" + Guid.NewGuid().ToString("N"));
                Window? secondary = null;
                try
                {
                    Directory.CreateDirectory(temporary);
                    var path = Path.Combine(temporary, "native-fixture.txt");
                    const string payload = "MySharp native service — 剪贴板 / ファイル";
                    await File.WriteAllTextAsync(path, payload);
                    UiContext? other = null;
                    secondary = MySharpApp.CreateWindow(cx => { other = cx; Ui.Text(cx, "Clipboard receiver"); },
                        new WindowOptions { Title = "MySharp Native Receiver", Width = 360, Height = 180, MinWidth = 320, MinHeight = 160 });
                    secondary.Show();
                    await Task.Delay(100);
                    var source = primary!.Clipboard ?? throw new Exception("Missing native clipboard.");
                    var receiver = other!.Clipboard ?? throw new Exception("Missing second-window clipboard.");
                    await source.SetTextAsync(payload);
                    if (await receiver.TryGetTextAsync() != payload) throw new Exception("Cross-window clipboard transfer failed.");
                    await receiver.SetTextAsync("reverse");
                    if (await source.TryGetTextAsync() != "reverse") throw new Exception("Reverse clipboard transfer failed.");
                    await source.ClearAsync();
                    if (!string.IsNullOrEmpty(await receiver.TryGetTextAsync())) throw new Exception("Clipboard clear failed.");
                    report["clipboard_roundtrip_and_clear"] = true;
                    var storage = primary.Storage ?? throw new Exception("Missing native storage provider.");
                    using (var file = await storage.TryGetFileFromPathAsync(path) ?? throw new Exception("Storage file lookup failed."))
                    {
                        using var stream = await file.OpenReadAsync();
                        using var reader = new StreamReader(stream);
                        if (await reader.ReadToEndAsync() != payload) throw new Exception("Storage read failed.");
                    }
                    using (var folder = await storage.TryGetFolderFromPathAsync(temporary) ?? throw new Exception("Storage folder lookup failed."))
                    {
                        var found = false;
                        await foreach (var item in folder.GetItemsAsync())
                        {
                            using (item) if (item.Name == "native-fixture.txt") found = true;
                        }
                        if (!found) throw new Exception("Storage enumeration failed.");
                        using var created = await folder.CreateFileAsync("created.txt") ?? throw new Exception("Storage creation failed.");
                        using (var stream = await created.OpenWriteAsync())
                        using (var writer = new StreamWriter(stream)) await writer.WriteAsync("created through storage");
                        if (await File.ReadAllTextAsync(Path.Combine(temporary, "created.txt")) != "created through storage")
                            throw new Exception("Storage write failed.");
                        await created.DeleteAsync();
                        if (File.Exists(Path.Combine(temporary, "created.txt"))) throw new Exception("Storage delete failed.");
                        report["storage_create_write_delete"] = true;
                    }
                    report["storage_lookup_read_enumerate"] = true;
                    var screenCount = primary.Screens?.All.Count ?? 0;
                    if (screenCount == 0) throw new Exception("Missing native screen information.");
                    report["screen_count"] = screenCount;
                    if (primary.PlatformHandle is not { Handle: var handle } || handle == IntPtr.Zero) throw new Exception("Missing native platform handle.");
                    report["platform_handle"] = true;
                    report["launcher_available"] = primary.Launcher is not null;
                    report["can_open_picker"] = storage.CanOpen;
                    if (picker)
                    {
                        await File.WriteAllTextAsync("artifacts/native-smoke-path.txt", path);
                        Console.WriteLine("Opening picker");
                        var selected = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
                        { Title = "MySharp native smoke picker", AllowMultiple = false });
                        Console.WriteLine($"Picker returned {selected.Count}");
                        try
                        {
                            if (selected.Count != 1) throw new Exception("Picker did not return one file.");
                            using var stream = await selected[0].OpenReadAsync();
                            using var reader = new StreamReader(stream);
                            if (await reader.ReadToEndAsync() != payload) throw new Exception("Picker selected wrong content.");
                            report["native_picker_selected_fixture"] = true;
                        }
                        finally { foreach (var file in selected) file.Dispose(); }
                    }
                }
                catch (Exception error) { report["error"] = error.ToString(); Environment.ExitCode = 1; }
                finally
                {
                    try { if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true); }
                    catch (Exception error) { report["cleanup_error"] = error.Message; Environment.ExitCode = 1; }
                    Directory.CreateDirectory("artifacts");
                    File.WriteAllText("artifacts/native-services-smoke.json", JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
                    Console.WriteLine("Report saved");
                    secondary?.Close();
                    ((IClassicDesktopStyleApplicationLifetime)Avalonia.Application.Current!.ApplicationLifetime!).Shutdown(Environment.ExitCode);
                }
            }
        }, args, configureApp: builder => builder.With(new X11PlatformOptions { UseDBusFilePicker = false }));
    }
}
