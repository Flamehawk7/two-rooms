using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using TwoRooms.Client.Game;
using TwoRooms.Hubs;

namespace TwoRooms.Services;

/// <summary>
/// Reaction Duel game logic, layered on top of the generic <see cref="Session"/> pairing shell.
/// Owns its own per-session state and pushes updates via the injected IHubContext, since round
/// timing fires from a background task after the originating hub method call has returned.
/// </summary>
public sealed class ReactionDuelService(IHubContext<GameHub, IGameHubClient> hubContext)
{
    private readonly ConcurrentDictionary<string, ReactionDuelState> _states = new();

    private ReactionDuelState GetOrCreate(string code) => _states.GetOrAdd(code, _ => new ReactionDuelState());

    public async Task SendCurrentState(Session session)
    {
        var state = GetOrCreate(session.Code);
        MatchScore score;
        lock (state.Lock)
        {
            score = state.Score;
        }
        await hubContext.Clients.Group(session.Code).OnDuelState(new DuelStateUpdate(session.Code, score));
    }

    public void OnPlayerDisconnected(Session session)
    {
        var state = GetOrCreate(session.Code);
        lock (state.Lock)
        {
            state.ResetRound();
        }
    }

    public async Task StartRound(Session session)
    {
        var state = GetOrCreate(session.Code);

        bool canStart;
        int delayMs;
        lock (state.Lock)
        {
            canStart = state.Phase == RoundPhase.Idle && session.BothConnected;
            delayMs = canStart ? session.Rng.Next(GameConstants.MinSignalDelayMs, GameConstants.MaxSignalDelayMs) : 0;
            if (canStart)
            {
                state.Phase = RoundPhase.Armed;
                state.RoundDecided = false;
                state.SignalStopwatch = null;
            }
        }

        if (!canStart) return;

        await hubContext.Clients.Group(session.Code).OnRoundArmed();

        _ = Task.Run(async () =>
        {
            await Task.Delay(delayMs);

            bool stillArmed;
            lock (state.Lock)
            {
                stillArmed = state.Phase == RoundPhase.Armed;
                if (stillArmed)
                {
                    state.Phase = RoundPhase.SignalGiven;
                    state.SignalStopwatch = Stopwatch.StartNew();
                }
            }

            if (stillArmed)
            {
                await hubContext.Clients.Group(session.Code).OnRoundSignal();
            }
        });
    }

    public async Task React(Session session, SessionSeat seat)
    {
        var state = GetOrCreate(session.Code);

        RoundResultMessage? roundResult = null;
        MatchOverMessage? matchOver = null;

        lock (state.Lock)
        {
            if (state.Phase == RoundPhase.Armed && !state.RoundDecided)
            {
                // Reacted before the signal: false start, opponent is awarded the round.
                state.RoundDecided = true;
                var opponent = Opponent(seat);
                state.AwardPoint(opponent);
                roundResult = new RoundResultMessage(opponent, true, seat, null, state.Score);
            }
            else if (state.Phase == RoundPhase.SignalGiven && !state.RoundDecided)
            {
                state.RoundDecided = true;
                var elapsedMs = state.SignalStopwatch!.ElapsedMilliseconds;
                state.AwardPoint(seat);
                roundResult = new RoundResultMessage(seat, false, null, elapsedMs, state.Score);
            }
            else
            {
                return; // Round already decided, or nothing to react to yet.
            }

            if (state.Score.WinsA >= GameConstants.WinsNeededForMatch || state.Score.WinsB >= GameConstants.WinsNeededForMatch)
            {
                var matchWinner = state.Score.WinsA > state.Score.WinsB ? SessionSeat.A : SessionSeat.B;
                state.CompleteMatch(matchWinner);
                matchOver = new MatchOverMessage(matchWinner, state.Score);
            }

            state.Phase = RoundPhase.Idle;
        }

        await hubContext.Clients.Group(session.Code).OnRoundResult(roundResult!);
        if (matchOver is not null)
        {
            await hubContext.Clients.Group(session.Code).OnMatchOver(matchOver);
        }
    }

    private static SessionSeat Opponent(SessionSeat seat) => seat == SessionSeat.A ? SessionSeat.B : SessionSeat.A;
}
