using System.Text.Json;
using System.Text.Json.Serialization;

namespace MySharp.Examples.Game2048;

public enum MoveDirection { Left, Up, Right, Down }
public sealed record NumberTile(int Id, long Value, int Row, int Column, bool Merged = false);
public sealed record GameSave(int Version, long Best, long Score, long[]? Cells, bool Won, bool KeepGoing);

// Independent line-compaction implementation of the classic 2048 rules.
public sealed class Game2048State
{
    private const long LargestTile = 1L << 50;
    private readonly Random random;
    private int nextId;
    private List<NumberTile> tiles = [];
    public IReadOnlyList<NumberTile> Tiles => tiles;
    public IReadOnlyList<NumberTile> Ghosts { get; private set; } = [];
    public long Score { get; private set; }
    public long Best { get; private set; }
    public long LastGain { get; private set; }
    public bool Won { get; private set; }
    public bool KeepGoing { get; private set; }
    public bool Over { get; private set; }
    public bool Stopped => Over || Won && !KeepGoing;
    public int MoveCount { get; private set; }
    public int SpawnedId { get; private set; }

    public Game2048State(Random? random = null)
    {
        this.random = random ?? new Random();
        Restart();
    }
    public void Restart()
    {
        tiles = []; Ghosts = []; Score = LastGain = 0;
        Won = KeepGoing = Over = false; MoveCount = 0;
        Spawn(); Spawn();
    }
    public bool Continue()
    {
        if (!Won || KeepGoing || Over) return false;
        KeepGoing = true; LastGain = 0; return true;
    }
    private void Spawn()
    {
        Span<int> empty = stackalloc int[16];
        var occupied = new bool[16];
        foreach (var tile in tiles) occupied[tile.Row * 4 + tile.Column] = true;
        var count = 0;
        for (var i = 0; i < 16; i++) if (!occupied[i]) empty[count++] = i;
        if (count == 0) return;
        var index = empty[random.Next(count)];
        SpawnedId = ++nextId;
        tiles.Add(new(SpawnedId, random.NextDouble() < .9 ? 2 : 4, index / 4, index % 4));
    }
    private static int Cell(MoveDirection direction, int line, int offset) => direction switch
    {
        MoveDirection.Left => line * 4 + offset,
        MoveDirection.Right => line * 4 + 3 - offset,
        MoveDirection.Up => offset * 4 + line,
        _ => (3 - offset) * 4 + line
    };
    private static bool Pair(NumberTile a, NumberTile b) => a.Value == b.Value && a.Value < LargestTile;
    public bool Move(MoveDirection direction)
    {
        if (Stopped) return false;
        var grid = new NumberTile?[16];
        foreach (var tile in tiles) grid[tile.Row * 4 + tile.Column] = tile;
        var result = new List<NumberTile>(16);
        var ghosts = new List<NumberTile>();
        var changed = false;
        long gained = 0;
        for (var line = 0; line < 4; line++)
        {
            var packed = new List<NumberTile>(4);
            for (var offset = 0; offset < 4; offset++)
                if (grid[Cell(direction, line, offset)] is {} tile) packed.Add(tile);
            var write = 0;
            for (var read = 0; read < packed.Count; read++)
            {
                var source = packed[read];
                var destination = Cell(direction, line, write++);
                var row = destination / 4; var column = destination % 4;
                if (read + 1 < packed.Count && Pair(source, packed[read + 1]))
                {
                    var other = packed[++read];
                    var value = source.Value * 2;
                    result.Add(new(++nextId, value, row, column, true));
                    ghosts.Add(source with { Row = row, Column = column });
                    ghosts.Add(other with { Row = row, Column = column });
                    gained += value; changed = true;
                }
                else
                {
                    changed |= source.Row != row || source.Column != column;
                    result.Add(source with { Row = row, Column = column, Merged = false });
                }
            }
        }
        if (!changed) return false;
        tiles = result; Ghosts = ghosts; LastGain = gained;
        Score += gained; Best = Math.Max(Best, Score); MoveCount++;
        Won |= tiles.Any(t => t.Value >= 2048);
        Spawn(); Over = !HasMoves();
        return true;
    }
    private bool HasMoves()
    {
        if (tiles.Count < 16) return true;
        var grid = new NumberTile?[16];
        foreach (var tile in tiles) grid[tile.Row * 4 + tile.Column] = tile;
        for (var i = 0; i < 16; i++)
        {
            if (i % 4 < 3 && Pair(grid[i]!, grid[i + 1]!)) return true;
            if (i < 12 && Pair(grid[i]!, grid[i + 4]!)) return true;
        }
        return false;
    }
    public GameSave Save()
    {
        var cells = new long[16];
        foreach (var tile in tiles) cells[tile.Row * 4 + tile.Column] = tile.Value;
        return new(1, Best, Score, Over ? null : cells, Won, KeepGoing);
    }
    public bool Restore(GameSave saved)
    {
        if (saved.Version != 1 || saved.Best < 0 || saved.Score < 0 || saved.Best > long.MaxValue / 2 || saved.Score > long.MaxValue / 2) return false;
        Best = Math.Max(saved.Best, Best);
        if (saved.Cells is not { Length: 16 } cells || cells.All(v => v == 0) || cells.Any(v => v < 0 || v > LargestTile || v != 0 && (v < 2 || (v & (v - 1)) != 0))) return false;
        tiles = [];
        for (var i = 0; i < 16; i++) if (cells[i] != 0) tiles.Add(new(++nextId, cells[i], i / 4, i % 4));
        Score = saved.Score; Best = Math.Max(Best, Score);
        Won = saved.Won || cells.Any(v => v >= 2048); KeepGoing = Won && saved.KeepGoing;
        Over = !HasMoves(); LastGain = 0; Ghosts = []; MoveCount = 0;
        return true;
    }
}

public sealed class Game2048Session
{
    private readonly string? path;
    public Game2048State Game { get; }
    public bool ReducedMotion { get; }
    public string? SaveError { get; private set; }
    public event Action<bool>? Changed;
    public Game2048Session(string? path = null, int? seed = null, bool reducedMotion = false)
    {
        this.path = path; ReducedMotion = reducedMotion;
        Game = new Game2048State(seed.HasValue ? new Random(seed.Value) : null);
        if (path is not null)
        {
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length <= 65536 && JsonSerializer.Deserialize(File.ReadAllText(path), Game2048JsonContext.Default.GameSave) is {} saved)
                    Game.Restore(saved);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { SaveError = error.Message; }
            Persist();
        }
    }
    public bool Move(MoveDirection direction)
    {
        if (!Game.Move(direction)) return false;
        Persist(); Changed?.Invoke(false); return true;
    }
    public void Restart() { Game.Restart(); Persist(); Changed?.Invoke(true); }
    public void Continue() { if (Game.Continue()) { Persist(); Changed?.Invoke(false); } }
    private void Persist()
    {
        if (path is null) return;
        string? temporary = null;
        try
        {
            var absolute = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            temporary = absolute + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(Game.Save(), Game2048JsonContext.Default.GameSave));
            File.Move(temporary, absolute, true); SaveError = null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { SaveError = error.Message; }
        finally { if (temporary is not null && File.Exists(temporary)) try { File.Delete(temporary); } catch (IOException) { } }
    }
}

[JsonSerializable(typeof(GameSave))]
internal partial class Game2048JsonContext : JsonSerializerContext { }
