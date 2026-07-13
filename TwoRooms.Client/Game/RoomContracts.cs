namespace TwoRooms.Client.Game;

public enum RoomShape
{
    Circle,
    Square,
    Triangle,
    Star
}

public enum RoomColor
{
    Red,
    Blue,
    Yellow,
    Green
}

public record RoomObjectDto(RoomShape Shape, RoomColor Color);

public record PlacedObjectDto(RoomShape Shape, RoomColor Color, int Row, int Col);

/// <summary>
/// Sent only to the Outside seat (A): the true object layout. Deliberately never includes the
/// Inside seat's current (possibly wrong) arrangement.
/// </summary>
public record RoomLayoutUpdate(
    string SessionCode,
    int Attempt,
    int Rows,
    int Cols,
    IReadOnlyList<PlacedObjectDto> Objects,
    bool Solved);

/// <summary>
/// Sent only to the Inside seat (B): their tray of unplaced objects and current placement.
/// Deliberately never includes the true layout.
/// </summary>
public record RoomArrangementUpdate(
    string SessionCode,
    int Attempt,
    int Rows,
    int Cols,
    IReadOnlyList<RoomObjectDto> Tray,
    IReadOnlyList<PlacedObjectDto> Placed,
    int CheckCount,
    bool Solved);

/// <summary>Broadcast to both seats after every check (partial or full match).</summary>
public record RoomCheckResultMessage(
    string SessionCode,
    int Attempt,
    int CorrectCount,
    int TotalCount,
    bool Solved);

/// <summary>Broadcast to both seats once the arrangement is fully correct.</summary>
public record RoomCompletedMessage(
    string SessionCode,
    int Attempt,
    long ElapsedMs,
    int CheckCount,
    long? BestCompletionMs);
