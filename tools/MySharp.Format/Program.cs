using MySharp.Formatting;

var check = false;
var width = 120;
var files = new List<string>();
try
{
    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--check": check = true; break;
            case "--width": width = int.Parse(args[++i]); break;
            default:
                if (args[i].StartsWith('-')) throw new ArgumentException($"Unknown option: {args[i]}");
                files.Add(Path.GetFullPath(args[i])); break;
        }
    }
    if (files.Count == 0) throw new ArgumentException("Usage: mysharp-format [--check] [--width 120] file.cs …");
    var staged = files.Distinct().Select(path =>
    {
        if (new FileInfo(path).LinkTarget != null) throw new ArgumentException($"Refusing symbolic link: {path}");
        var original = File.ReadAllText(path);
        return (path, original, formatted: CompactFormatter.Format(original, width));
    }).Where(f => f.original != f.formatted).ToArray();
    // Parse and format every file before writing any, including in check mode.
    foreach (var file in staged)
        if (File.ReadAllText(file.path) != file.original) throw new IOException($"File changed: {file.path}");
    foreach (var file in staged)
    {
        Console.WriteLine($"{(check ? "Would format" : "Formatted")} {file.path}");
        if (check) continue;
        var temp = file.path + ".mysharp-format-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temp, file.formatted);
            File.Move(temp, file.path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    return check && staged.Length > 0 ? 1 : 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    return 2;
}
