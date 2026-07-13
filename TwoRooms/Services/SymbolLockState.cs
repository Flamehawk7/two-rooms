using TwoRooms.Client.Game;

namespace TwoRooms.Services;

/// <summary>
/// Per-session Symbol-Matching Lock state. A "lock" (attempt) has 1 stage (Single difficulty) or
/// 3 escalating stages (MultiStage). Each stage picks a random symbol pool, assigns each symbol a
/// random digit (the codex), and picks a door sequence by sampling the pool with repetition. The
/// code is the digits of the door sequence looked up in the codex.
/// </summary>
public sealed class SymbolLockState
{
    public static readonly char[] MasterSymbols = ['★', '●', '▲', '■', '◆', '☾', '☀', '✦'];

    public object Lock { get; } = new();
    public LockDifficulty Difficulty { get; private set; } = LockDifficulty.Single;
    public int Attempt { get; private set; }
    public int Stage { get; private set; } = 1;
    public int TotalStages { get; private set; } = 1;
    public IReadOnlyList<char> Pool { get; private set; } = [];
    public IReadOnlyDictionary<char, int> Codex { get; private set; } = new Dictionary<char, int>();
    public IReadOnlyList<char> DoorSequence { get; private set; } = [];
    public bool StageSolved { get; private set; }
    public bool AllStagesSolved { get; private set; }
    public int StageMistakeCount { get; private set; }
    public int TotalMistakeCount { get; private set; }
    public DateTime AttemptStartedAtUtc { get; private set; }
    public long? BestCompletionMs { get; private set; }

    public long TotalElapsedMs => (long)(DateTime.UtcNow - AttemptStartedAtUtc).TotalMilliseconds;
    public string ExpectedCode => string.Concat(DoorSequence.Select(s => Codex[s]));

    public static (int PoolSize, int SequenceLength) DifficultyForStage(LockDifficulty difficulty, int stage) =>
        difficulty == LockDifficulty.Single
            ? (6, 5)
            : stage switch
            {
                1 => (4, 3),
                2 => (6, 4),
                _ => (8, 5)
            };

    /// <summary>Starts a brand-new lock (attempt), resetting stage/mistake tracking. Must be called under <see cref="Lock"/>.</summary>
    public void BeginAttempt(LockDifficulty difficulty, Random rng)
    {
        Attempt++;
        Difficulty = difficulty;
        TotalStages = difficulty == LockDifficulty.Single ? 1 : 3;
        TotalMistakeCount = 0;
        AllStagesSolved = false;
        AttemptStartedAtUtc = DateTime.UtcNow;
        GenerateStage(1, rng);
    }

    /// <summary>Advances from a solved stage to the next one. Must be called under <see cref="Lock"/>.</summary>
    public void AdvanceStage(Random rng) => GenerateStage(Stage + 1, rng);

    private void GenerateStage(int stage, Random rng)
    {
        Stage = stage;
        StageSolved = false;
        StageMistakeCount = 0;

        var (poolSize, sequenceLength) = DifficultyForStage(Difficulty, stage);
        Pool = MasterSymbols.OrderBy(_ => rng.Next()).Take(poolSize).ToArray();
        Codex = Pool.ToDictionary(s => s, _ => rng.Next(0, 10));
        DoorSequence = Enumerable.Range(0, sequenceLength).Select(_ => Pool[rng.Next(Pool.Count)]).ToArray();
    }

    /// <summary>Attempts to solve the current stage. Returns true if the code was correct. Must be called under <see cref="Lock"/>.</summary>
    public bool TrySubmit(string code)
    {
        if (StageSolved) return false;

        if (code != ExpectedCode)
        {
            StageMistakeCount++;
            TotalMistakeCount++;
            return false;
        }

        StageSolved = true;
        if (Stage >= TotalStages)
        {
            AllStagesSolved = true;
            var elapsed = TotalElapsedMs;
            if (BestCompletionMs is null || elapsed < BestCompletionMs) BestCompletionMs = elapsed;
        }

        return true;
    }
}
