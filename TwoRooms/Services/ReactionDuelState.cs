using System.Diagnostics;
using TwoRooms.Client.Game;

namespace TwoRooms.Services;

/// <summary>Server-only round phase; never sent to clients (they track their own UI stage locally).</summary>
public enum RoundPhase
{
    Idle,
    Armed,
    SignalGiven
}

/// <summary>Per-session Reaction Duel state: round phase, signal timing, and match score.</summary>
public sealed class ReactionDuelState
{
    public object Lock { get; } = new();
    public RoundPhase Phase { get; set; } = RoundPhase.Idle;
    public bool RoundDecided { get; set; }
    public Stopwatch? SignalStopwatch { get; set; }

    private int _winsA;
    private int _winsB;
    private int _matchesWonA;
    private int _matchesWonB;

    public MatchScore Score => new(_winsA, _winsB, _matchesWonA, _matchesWonB);

    public void AwardPoint(SessionSeat seat)
    {
        if (seat == SessionSeat.A) _winsA++; else _winsB++;
    }

    public void CompleteMatch(SessionSeat winner)
    {
        if (winner == SessionSeat.A) _matchesWonA++; else _matchesWonB++;
        _winsA = 0;
        _winsB = 0;
    }

    public void ResetRound()
    {
        Phase = RoundPhase.Idle;
        RoundDecided = false;
        SignalStopwatch = null;
    }
}
