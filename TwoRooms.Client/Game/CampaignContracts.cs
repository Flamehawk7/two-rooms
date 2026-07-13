namespace TwoRooms.Client.Game;

public enum CampaignGameKind
{
    Maze,
    SymbolLock,
    Bomb,
    Room
}

/// <summary>
/// Orchestration-level state: which games are in this run, in what order, and how far along.
/// Same for both seats (unlike every other game's state) -- the asymmetry lives inside whichever
/// stage is currently active, not at this level.
/// </summary>
public record CampaignStateUpdate(
    string SessionCode,
    int Attempt,
    IReadOnlyList<CampaignGameKind> StageOrder,
    int CurrentStageIndex,
    bool Started,
    bool Completed,
    long StartedAtUnixMs,
    long? CompletedElapsedMs,
    long? BestCompletionMs);
