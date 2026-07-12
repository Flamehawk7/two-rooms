namespace TwoRooms.Client.Game;

public enum MazeDirection
{
    North,
    East,
    South,
    West
}

/// <summary>Which walls are open (passable) around one cell.</summary>
public record MazeCellDto(bool North, bool East, bool South, bool West);

/// <summary>
/// Sent only to the Guide (seat A): the full static maze layout. Deliberately never includes the
/// Navigator's current position &mdash; the Guide has to be told where their partner is out loud.
/// </summary>
public record MazeLayoutUpdate(
    string SessionCode,
    int Attempt,
    int Width,
    int Height,
    IReadOnlyList<MazeCellDto> Cells,
    int StartX,
    int StartY,
    int ExitX,
    int ExitY,
    bool Solved);

/// <summary>
/// Sent only to the Navigator (seat B): where they are and which directions are open from here.
/// Deliberately never includes the overall maze layout.
/// </summary>
public record MazePositionUpdate(
    string SessionCode,
    int Attempt,
    int X,
    int Y,
    MazeCellDto Surroundings,
    int MoveCount,
    int BumpCount,
    bool Solved);

/// <summary>Broadcast to both seats once the maze is solved.</summary>
public record MazeCompletedMessage(
    string SessionCode,
    int Attempt,
    long CompletionMs,
    int MoveCount,
    int BumpCount,
    long? BestCompletionMs);
