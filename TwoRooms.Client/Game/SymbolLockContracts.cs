namespace TwoRooms.Client.Game;

public enum LockDifficulty
{
    /// <summary>One stage, moderate pool/sequence size.</summary>
    Single,

    /// <summary>Three stages, escalating pool size and sequence length each stage.</summary>
    MultiStage
}

public record SymbolCodexEntry(string Symbol, int Digit);

/// <summary>
/// Sent only to the Reader (seat A): the door's symbol sequence for the current stage.
/// Deliberately never includes the codex mapping needed to decode it.
/// </summary>
public record DoorViewUpdate(
    string SessionCode,
    LockDifficulty Difficulty,
    int Stage,
    int TotalStages,
    IReadOnlyList<string> DoorSymbols,
    bool StageSolved,
    bool AllStagesSolved);

/// <summary>
/// Sent only to the Codex Keeper (seat B): the symbol-&gt;digit mapping for the current stage.
/// Deliberately never includes the door's symbol sequence.
/// </summary>
public record CodexViewUpdate(
    string SessionCode,
    LockDifficulty Difficulty,
    int Stage,
    int TotalStages,
    IReadOnlyList<SymbolCodexEntry> Codex,
    int StageMistakeCount,
    int TotalMistakeCount,
    bool StageSolved,
    bool AllStagesSolved);

/// <summary>Broadcast to both seats after every code submission.</summary>
public record SymbolLockStageResultMessage(
    string SessionCode,
    int Stage,
    int TotalStages,
    bool Correct,
    int StageMistakeCount,
    bool AllStagesSolved);

/// <summary>Broadcast to both seats once the final stage is solved.</summary>
public record SymbolLockCompletedMessage(
    string SessionCode,
    LockDifficulty Difficulty,
    int TotalStages,
    long TotalElapsedMs,
    int TotalMistakeCount,
    long? BestCompletionMs);
