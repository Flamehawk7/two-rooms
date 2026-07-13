using TwoRooms.Client.Game;

namespace TwoRooms.Services;

public readonly record struct RoomPlacement(RoomShape Shape, RoomColor Color, int Row, int Col);
internal readonly record struct RoomObject(RoomShape Shape, RoomColor Color);
internal readonly record struct RoomCell(int Row, int Col);

/// <summary>
/// Per-session Room Description Puzzle state. Generates a random subset of unique (shape, color)
/// objects placed in random, non-overlapping grid cells. The Inside seat places objects into an
/// initially-empty copy of the grid; a full match against the true layout solves it.
/// </summary>
public sealed class RoomState
{
    public const int Rows = 3;
    public const int Cols = 4;
    public const int ObjectCount = 6;

    public object Lock { get; } = new();
    public int Attempt { get; private set; }
    public IReadOnlyList<RoomPlacement> TrueObjects { get; private set; } = [];
    public bool Solved { get; private set; }
    public int CheckCount { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public long? BestCompletionMs { get; private set; }

    private Dictionary<RoomCell, RoomObject> _placed = new();

    public long ElapsedMs => (long)(DateTime.UtcNow - StartedAtUtc).TotalMilliseconds;

    public IReadOnlyList<PlacedObjectDto> PlacedObjects =>
        _placed.Select(kv => new PlacedObjectDto(kv.Value.Shape, kv.Value.Color, kv.Key.Row, kv.Key.Col)).ToList();

    public IReadOnlyList<RoomObjectDto> Tray
    {
        get
        {
            var placedObjects = _placed.Values.ToHashSet();
            return TrueObjects
                .Select(o => new RoomObject(o.Shape, o.Color))
                .Where(o => !placedObjects.Contains(o))
                .Select(o => new RoomObjectDto(o.Shape, o.Color))
                .ToList();
        }
    }

    /// <summary>Carves a brand-new room in place. Must be called under <see cref="Lock"/>.</summary>
    public void Generate(Random rng)
    {
        Attempt++;
        Solved = false;
        CheckCount = 0;
        StartedAtUtc = DateTime.UtcNow;
        _placed = new Dictionary<RoomCell, RoomObject>();

        var combos = Enum.GetValues<RoomShape>()
            .SelectMany(s => Enum.GetValues<RoomColor>().Select(c => new RoomObject(s, c)))
            .ToList();
        Shuffle(combos, rng);
        var chosenObjects = combos.Take(ObjectCount).ToList();

        var cells = new List<RoomCell>();
        for (var r = 0; r < Rows; r++)
        for (var c = 0; c < Cols; c++)
            cells.Add(new RoomCell(r, c));
        Shuffle(cells, rng);
        var chosenCells = cells.Take(ObjectCount).ToList();

        TrueObjects = chosenObjects
            .Zip(chosenCells, (o, cell) => new RoomPlacement(o.Shape, o.Color, cell.Row, cell.Col))
            .ToList();
    }

    /// <summary>Attempts to place an object into an empty cell. Must be called under <see cref="Lock"/>.</summary>
    public bool TryPlace(RoomShape shape, RoomColor color, int row, int col)
    {
        if (Solved) return false;
        if (row < 0 || row >= Rows || col < 0 || col >= Cols) return false;

        var cell = new RoomCell(row, col);
        if (_placed.ContainsKey(cell)) return false;

        var obj = new RoomObject(shape, color);
        if (!TrueObjects.Any(o => o.Shape == shape && o.Color == color)) return false; // not an object in this room
        if (_placed.ContainsValue(obj)) return false; // already placed somewhere else

        _placed[cell] = obj;
        return true;
    }

    /// <summary>Picks an object back up off the grid into the tray. Must be called under <see cref="Lock"/>.</summary>
    public bool TryRemove(int row, int col)
    {
        if (Solved) return false;
        return _placed.Remove(new RoomCell(row, col));
    }

    /// <summary>Checks the current arrangement against the truth. Must be called under <see cref="Lock"/>.</summary>
    public (int Correct, int Total) Check()
    {
        CheckCount++;
        var correct = TrueObjects.Count(o =>
            _placed.TryGetValue(new RoomCell(o.Row, o.Col), out var placed) &&
            placed.Shape == o.Shape && placed.Color == o.Color);

        if (correct == ObjectCount)
        {
            Solved = true;
            var elapsed = ElapsedMs;
            if (BestCompletionMs is null || elapsed < BestCompletionMs) BestCompletionMs = elapsed;
        }

        return (correct, ObjectCount);
    }

    private static void Shuffle<T>(IList<T> list, Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
