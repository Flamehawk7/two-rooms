namespace TwoRooms.Client.Game;

/// <summary>Which of the two seats in a session a player occupies.</summary>
public enum SessionSeat
{
    A,
    B
}

public record JoinSessionResult(bool Success, string? Error, SessionSeat Seat, string SessionCode);

public record PlayerInfo(string Name, SessionSeat Seat, bool IsConnected);

public record MatchScore(int WinsA, int WinsB, int MatchesWonA, int MatchesWonB)
{
    public static readonly MatchScore Empty = new(0, 0, 0, 0);
}

/// <summary>Generic, game-agnostic session membership update: who's in the session and whether both seats are live.</summary>
public record SessionStateUpdate(
    string SessionCode,
    IReadOnlyList<PlayerInfo> Players,
    bool BothConnected);

/// <summary>Current Reaction Duel score, pushed on (re)join so a reconnecting client isn't stuck at 0-0 until the next round ends.</summary>
public record DuelStateUpdate(string SessionCode, MatchScore Score);

public record RoundResultMessage(
    SessionSeat? Winner,
    bool FalseStart,
    SessionSeat? FalseStartBy,
    long? ReactionTimeMs,
    MatchScore Score);

public record MatchOverMessage(SessionSeat Winner, MatchScore Score);

public static class GameConstants
{
    public const string HubPath = "/hubs/game";
    public const int WinsNeededForMatch = 3;
    public const int MinSignalDelayMs = 1500;
    public const int MaxSignalDelayMs = 4000;
}
