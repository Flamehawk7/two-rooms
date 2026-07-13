using TwoRooms.Client.Game;

namespace TwoRooms.Services;

public sealed class BombRule
{
    public required WireColor Color { get; init; }
    public required int Count { get; init; }
    public required int Ordinal { get; init; }
    public required bool IsReal { get; init; }
}

/// <summary>
/// Per-session Bomb-Defusal state. Generates a wire layout and a small "manual" of conditional
/// rules ("if there are N {color} wires, cut the Kth such wire"), evaluated in order; exactly one
/// rule is constructed to match the actual layout (the rest are guaranteed non-matching
/// distractors), so the manual always has a well-defined single correct answer.
/// </summary>
public sealed class BombState
{
    public const int WireCount = 6;
    public const int RuleCount = 4;
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(90);

    public object Lock { get; } = new();
    public int Attempt { get; private set; }
    public IReadOnlyList<WireColor> Wires { get; private set; } = [];
    public IReadOnlyList<BombRule> Rules { get; private set; } = [];
    public int CorrectWireIndex { get; private set; }
    public bool Resolved { get; private set; }
    public bool Defused { get; private set; }
    public BombFailureReason? FailureReason { get; private set; }
    public int? CutWireIndex { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public long? BestCompletionMs { get; private set; }

    public long ElapsedMs => (long)(DateTime.UtcNow - StartedAtUtc).TotalMilliseconds;

    /// <summary>Carves a brand-new bomb in place. Must be called under <see cref="Lock"/>.</summary>
    public void Generate(Random rng)
    {
        Attempt++;
        Resolved = false;
        Defused = false;
        FailureReason = null;
        CutWireIndex = null;
        StartedAtUtc = DateTime.UtcNow;

        var colors = Enum.GetValues<WireColor>();
        var wires = new WireColor[WireCount];
        for (var i = 0; i < WireCount; i++) wires[i] = colors[rng.Next(colors.Length)];
        Wires = wires;

        var counts = colors.ToDictionary(c => c, c => wires.Count(w => w == c));
        var presentColors = colors.Where(c => counts[c] > 0).ToArray();

        var realIndex = rng.Next(RuleCount);
        var rules = new BombRule[RuleCount];
        for (var i = 0; i < RuleCount; i++)
        {
            if (i == realIndex)
            {
                var color = presentColors[rng.Next(presentColors.Length)];
                var count = counts[color];
                var ordinal = rng.Next(1, count + 1);
                rules[i] = new BombRule { Color = color, Count = count, Ordinal = ordinal, IsReal = true };
                CorrectWireIndex = NthIndexOfColor(wires, color, ordinal);
            }
            else
            {
                var color = colors[rng.Next(colors.Length)];
                int count;
                do
                {
                    count = rng.Next(0, WireCount + 1);
                } while (count == counts[color]);
                var ordinal = rng.Next(1, count + 1);
                rules[i] = new BombRule { Color = color, Count = count, Ordinal = ordinal, IsReal = false };
            }
        }
        Rules = rules;
    }

    /// <summary>Attempts to cut the wire at <paramref name="position"/>. Must be called under <see cref="Lock"/>.</summary>
    public bool TryCut(int position)
    {
        if (Resolved) return false;

        CutWireIndex = position;
        Resolved = true;

        if (position == CorrectWireIndex)
        {
            Defused = true;
            var elapsed = ElapsedMs;
            if (BestCompletionMs is null || elapsed < BestCompletionMs) BestCompletionMs = elapsed;
            return true;
        }

        Defused = false;
        FailureReason = BombFailureReason.WrongWire;
        return false;
    }

    /// <summary>Marks the bomb as timed out, if not already resolved. Must be called under <see cref="Lock"/>.</summary>
    public bool TryExpire()
    {
        if (Resolved) return false;
        Resolved = true;
        Defused = false;
        FailureReason = BombFailureReason.TimedOut;
        return true;
    }

    private static int NthIndexOfColor(WireColor[] wires, WireColor color, int ordinal)
    {
        var seen = 0;
        for (var i = 0; i < wires.Length; i++)
        {
            if (wires[i] != color) continue;
            seen++;
            if (seen == ordinal) return i;
        }
        throw new InvalidOperationException("Ordinal exceeds the number of occurrences of that color.");
    }
}
