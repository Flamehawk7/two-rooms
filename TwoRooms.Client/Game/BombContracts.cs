namespace TwoRooms.Client.Game;

public enum WireColor
{
    Red,
    Blue,
    Yellow,
    Black
}

public enum BombFailureReason
{
    WrongWire,
    TimedOut
}

/// <summary>One manual rule: "If there are Count {Color} wires, cut the Ordinal-th such wire."</summary>
public record BombRuleDto(WireColor Color, int Count, int Ordinal);

/// <summary>
/// Sent only to the Wire Viewer (seat A): the wire colors in position order. Deliberately never
/// includes the manual needed to know which one is correct.
/// </summary>
public record WireDiagramUpdate(
    string SessionCode,
    int Attempt,
    IReadOnlyList<WireColor> WireColors,
    long StartedAtUnixMs,
    long DurationMs,
    bool Resolved,
    bool? Defused);

/// <summary>
/// Sent only to the Manual Holder (seat B): the rule manual. Deliberately never includes the
/// actual wire colors needed to evaluate it.
/// </summary>
public record ManualUpdate(
    string SessionCode,
    int Attempt,
    IReadOnlyList<BombRuleDto> Rules,
    long StartedAtUnixMs,
    long DurationMs,
    bool Resolved,
    bool? Defused);

/// <summary>Broadcast to both seats once the bomb is defused, cut wrong, or the timer expires.</summary>
public record BombResultMessage(
    string SessionCode,
    int Attempt,
    bool Defused,
    BombFailureReason? FailureReason,
    int? CutWirePosition,
    long ElapsedMs,
    long? BestCompletionMs);
