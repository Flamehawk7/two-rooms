using TwoRooms.Client.Game;

namespace TwoRooms.Services;

/// <summary>
/// Per-session Multi-Room Campaign state: a shuffled order of the four asymmetric puzzle games,
/// a current stage pointer, and a global timer. Same for both seats -- this is pure bookkeeping,
/// the actual gameplay asymmetry lives inside whichever game each stage delegates to.
/// </summary>
public sealed class CampaignState
{
    public object Lock { get; } = new();
    public int Attempt { get; private set; }
    public IReadOnlyList<CampaignGameKind> StageOrder { get; private set; } = [];
    public int CurrentStageIndex { get; private set; }
    public bool Started { get; private set; }
    public bool Completed { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public long? CompletedElapsedMs { get; private set; }
    public long? BestCompletionMs { get; private set; }

    public CampaignGameKind CurrentGame => StageOrder[CurrentStageIndex];

    /// <summary>Begins a brand-new run with a freshly shuffled stage order. Must be called under <see cref="Lock"/>.</summary>
    public void Start(Random rng)
    {
        Attempt++;
        var kinds = Enum.GetValues<CampaignGameKind>().ToList();
        Shuffle(kinds, rng);
        StageOrder = kinds;
        CurrentStageIndex = 0;
        Started = true;
        Completed = false;
        CompletedElapsedMs = null;
        StartedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Moves to the next stage, or marks the run complete if that was the last one. Must be called under <see cref="Lock"/>.</summary>
    public bool Advance()
    {
        if (!Started || Completed) return false;

        CurrentStageIndex++;
        if (CurrentStageIndex >= StageOrder.Count)
        {
            Completed = true;
            var elapsed = (long)(DateTime.UtcNow - StartedAtUtc).TotalMilliseconds;
            CompletedElapsedMs = elapsed;
            if (BestCompletionMs is null || elapsed < BestCompletionMs) BestCompletionMs = elapsed;
        }

        return true;
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
