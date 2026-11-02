using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MySharp;

internal static class TypographySmoke
{
    internal static void Run(string directory)
    {
        var sizes = new[] { 12, 14, 16 };
        var modes = new[] { "原图", "译文", "对照" };
        Directory.CreateDirectory(directory);
        MySharpApp.Run(cx => Ui.Column(cx).Gap(12).Padding(24).Children(() =>
        {
            Ui.Text(cx, "Typography · 字体与控件").FontSize(20).Bold();
            foreach (var size in sizes) cx.Scope(size, () =>
            {
                Ui.Text(cx, "Settings 设置 · 漫画翻译 0123").FontSize(size);
                Ui.Row(cx).Gap(8).Children(() =>
                {
                    Ui.TextInput(cx, "文本校对 Text input", _ => { }).FontSize(size).Width(340);
                    Ui.Button(cx, "保存设置 Save").FontSize(size);
                });
            });
            var choice = cx.State(0);
            Ui.Segmented(cx, modes, choice).Label("预览方式");
        }), new WindowOptions
        {
            Title = "MySharp Typography Smoke", Width = 720, Height = 460,
            Opened = window =>
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    try
                    {
                        window.UpdateLayout();
                        var samples = window.GetVisualDescendants().OfType<TextBlock>()
                            .Where(t => t.Text?.StartsWith("Settings 设置") == true).Select(t => new
                            {
                                size = t.FontSize, family = t.FontFamily.ToString(),
                                runs = t.TextLayout.TextLines.SelectMany(l => l.TextRuns).OfType<ShapedTextRun>()
                                    .Select(r => new { family = r.GlyphRun.GlyphTypeface.FamilyName, weight = r.GlyphRun.GlyphTypeface.Weight.ToString(), missing = r.GlyphRun.GlyphInfos.Any(g => g.GlyphIndex == 0) }).ToArray()
                            }).ToArray();
                        if (samples.Length != 3 || samples.Any(s => s.runs.Any(r => r.missing))) throw new Exception("Typography sample contains missing glyphs.");
                        if (OperatingSystem.IsLinux() && FontManager.Current.SystemFonts.Any(f => f.Name == "Noto Sans CJK SC") &&
                            samples.Any(s => !s.runs.Any(r => r.family == "Noto Sans CJK SC"))) throw new Exception("Installed CJK UI font was not selected.");
                        var scale = window.RenderScaling;
                        var report = new { render_scale = scale, samples };
                        File.WriteAllText(Path.Combine(directory, "typography.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
                        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.Bounds.Width * scale), (int)Math.Ceiling(window.Bounds.Height * scale)), new Vector(96 * scale, 96 * scale));
                        bitmap.Render(window);
                        bitmap.Save(Path.Combine(directory, "typography.png"), PngBitmapEncoderOptions.Default);
                        Console.WriteLine(JsonSerializer.Serialize(report));
                    }
                    catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
                    finally { window.Close(); }
                };
                timer.Start();
            }
        });
    }
}
