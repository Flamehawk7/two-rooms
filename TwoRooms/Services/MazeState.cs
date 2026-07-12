using TwoRooms.Client.Game;

namespace TwoRooms.Services;

public sealed class MazeCell
{
    public bool North;
    public bool East;
    public bool South;
    public bool West;

    public bool IsOpen(MazeDirection direction) => direction switch
    {
        MazeDirection.North => North,
        MazeDirection.East => East,
        MazeDirection.South => South,
        MazeDirection.West => West,
        _ => false
    };
}

/// <summary>
/// Per-session Maze Navigator state: a perfect maze (single path between any two cells) carved
/// with a randomized depth-first backtracker, plus the Navigator's current position and stats.
/// </summary>
public sealed class MazeState(int width, int height)
{
    public object Lock { get; } = new();
    public int Width { get; } = width;
    public int Height { get; } = height;
    public int Attempt { get; private set; }
    public MazeCell[,] Cells { get; private set; } = new MazeCell[width, height];
    public (int X, int Y) Start { get; private set; }
    public (int X, int Y) Exit { get; private set; }
    public (int X, int Y) PlayerPos { get; private set; }
    public bool Solved { get; private set; }
    public int MoveCount { get; private set; }
    public int BumpCount { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public long? BestCompletionMs { get; private set; }

    /// <summary>Carves a brand-new maze in place. Must be called under <see cref="Lock"/>.</summary>
    public void Generate(Random rng)
    {
        Attempt++;
        Cells = new MazeCell[Width, Height];
        for (var x = 0; x < Width; x++)
        for (var y = 0; y < Height; y++)
            Cells[x, y] = new MazeCell();

        Start = (0, 0);
        Exit = (Width - 1, Height - 1);

        var visited = new bool[Width, Height];
        var stack = new Stack<(int X, int Y)>();
        stack.Push(Start);
        visited[Start.X, Start.Y] = true;

        while (stack.Count > 0)
        {
            var (cx, cy) = stack.Peek();
            var neighbors = UnvisitedNeighbors(cx, cy, visited);
            if (neighbors.Count == 0)
            {
                stack.Pop();
                continue;
            }

            var (nx, ny, dir) = neighbors[rng.Next(neighbors.Count)];
            Carve(cx, cy, nx, ny, dir);
            visited[nx, ny] = true;
            stack.Push((nx, ny));
        }

        PlayerPos = Start;
        Solved = false;
        MoveCount = 0;
        BumpCount = 0;
        StartedAtUtc = DateTime.UtcNow;
    }

    public MazeCell CurrentCell => Cells[PlayerPos.X, PlayerPos.Y];

    /// <summary>Attempts to move; returns true if it succeeded, false if it bumped a wall. Must be called under <see cref="Lock"/>.</summary>
    public bool TryMove(MazeDirection direction)
    {
        if (Solved) return false;

        if (!CurrentCell.IsOpen(direction))
        {
            BumpCount++;
            return false;
        }

        var (x, y) = PlayerPos;
        PlayerPos = direction switch
        {
            MazeDirection.North => (x, y - 1),
            MazeDirection.East => (x + 1, y),
            MazeDirection.South => (x, y + 1),
            MazeDirection.West => (x - 1, y),
            _ => PlayerPos
        };
        MoveCount++;

        if (PlayerPos == Exit)
        {
            Solved = true;
            var elapsedMs = (long)(DateTime.UtcNow - StartedAtUtc).TotalMilliseconds;
            if (BestCompletionMs is null || elapsedMs < BestCompletionMs)
            {
                BestCompletionMs = elapsedMs;
            }
        }

        return true;
    }

    public long ElapsedMs => (long)(DateTime.UtcNow - StartedAtUtc).TotalMilliseconds;

    private List<(int X, int Y, MazeDirection Dir)> UnvisitedNeighbors(int x, int y, bool[,] visited)
    {
        var list = new List<(int, int, MazeDirection)>(4);
        if (y > 0 && !visited[x, y - 1]) list.Add((x, y - 1, MazeDirection.North));
        if (x < Width - 1 && !visited[x + 1, y]) list.Add((x + 1, y, MazeDirection.East));
        if (y < Height - 1 && !visited[x, y + 1]) list.Add((x, y + 1, MazeDirection.South));
        if (x > 0 && !visited[x - 1, y]) list.Add((x - 1, y, MazeDirection.West));
        return list;
    }

    private void Carve(int x, int y, int nx, int ny, MazeDirection dir)
    {
        switch (dir)
        {
            case MazeDirection.North:
                Cells[x, y].North = true;
                Cells[nx, ny].South = true;
                break;
            case MazeDirection.East:
                Cells[x, y].East = true;
                Cells[nx, ny].West = true;
                break;
            case MazeDirection.South:
                Cells[x, y].South = true;
                Cells[nx, ny].North = true;
                break;
            case MazeDirection.West:
                Cells[x, y].West = true;
                Cells[nx, ny].East = true;
                break;
        }
    }
}
