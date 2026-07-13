using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using TwoRooms.Client.Game;
using TwoRooms.Hubs;

namespace TwoRooms.Services;

/// <summary>
/// Bomb-Defusal game logic, layered on top of the generic <see cref="Session"/> pairing shell.
/// The Wire Viewer (seat A) gets the wire colors but never the manual; the Manual Holder (seat B)
/// gets the rule manual but never the wire colors. Unlike Maze/Symbol Lock, a wrong action here is
/// a real failure (not a soft retry) and the countdown is enforced server-side, not just displayed.
/// </summary>
public sealed class BombService(IHubContext<GameHub, IGameHubClient> hubContext)
{
    private readonly ConcurrentDictionary<string, BombState> _states = new();

    private BombState GetOrCreate(Session session)
    {
        return _states.GetOrAdd(session.Code, code =>
        {
            var state = new BombState();
            lock (state.Lock)
            {
                state.Generate(SeededRng(code, attempt: 1));
            }
            ScheduleTimeout(session, state, state.Attempt);
            return state;
        });
    }

    public async Task SendStateToSeat(Session session, SessionSeat seat)
    {
        var connectionId = session.ConnectionIdOf(seat);
        if (connectionId is null) return;

        var state = GetOrCreate(session);
        if (seat == SessionSeat.A)
        {
            WireDiagramUpdate view;
            lock (state.Lock)
            {
                view = BuildWireView(session.Code, state);
            }
            await hubContext.Clients.Client(connectionId).OnBombWires(view);
        }
        else
        {
            ManualUpdate view;
            lock (state.Lock)
            {
                view = BuildManualView(session.Code, state);
            }
            await hubContext.Clients.Client(connectionId).OnBombManual(view);
        }
    }

    public async Task CutWire(Session session, SessionSeat seat, int position)
    {
        if (seat != SessionSeat.B) return; // only the Manual Holder cuts

        var state = GetOrCreate(session);
        BombResultMessage? result = null;

        lock (state.Lock)
        {
            if (position < 0 || position >= BombState.WireCount) return;
            if (state.Resolved) return; // already resolved (defused/failed/timed out); ignore further cuts

            var defused = state.TryCut(position);
            result = new BombResultMessage(
                session.Code, state.Attempt, defused, state.FailureReason, position, state.ElapsedMs, state.BestCompletionMs);
        }

        await hubContext.Clients.Group(session.Code).OnBombResult(result);
    }

    public async Task NewBomb(Session session)
    {
        var state = GetOrCreate(session);
        int attempt;
        lock (state.Lock)
        {
            attempt = state.Attempt + 1;
        }
        lock (state.Lock)
        {
            state.Generate(SeededRng(session.Code, attempt));
        }
        ScheduleTimeout(session, state, attempt);

        await SendStateToSeat(session, SessionSeat.A);
        await SendStateToSeat(session, SessionSeat.B);
    }

    private void ScheduleTimeout(Session session, BombState state, int attemptAtSchedule)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(BombState.Duration);

            BombResultMessage? result = null;
            lock (state.Lock)
            {
                // A New Bomb started in the meantime must not let this stale timer expire it.
                if (state.Attempt == attemptAtSchedule && state.TryExpire())
                {
                    result = new BombResultMessage(
                        session.Code, state.Attempt, false, state.FailureReason, state.CutWireIndex, state.ElapsedMs, state.BestCompletionMs);
                }
            }

            if (result is not null)
            {
                await hubContext.Clients.Group(session.Code).OnBombResult(result);
            }
        });
    }

    /// <summary>Same session code + attempt always carves the same bomb, independent of any other game's RNG use.</summary>
    private static Random SeededRng(string sessionCode, int attempt) =>
        new(Session.StableHash($"{sessionCode}:bomb:{attempt}"));

    private static WireDiagramUpdate BuildWireView(string code, BombState state) => new(
        code, state.Attempt, state.Wires, ToUnixMs(state.StartedAtUtc), (long)BombState.Duration.TotalMilliseconds,
        state.Resolved, state.Resolved ? state.Defused : null);

    private static ManualUpdate BuildManualView(string code, BombState state) => new(
        code, state.Attempt,
        state.Rules.Select(r => new BombRuleDto(r.Color, r.Count, r.Ordinal)).ToList(),
        ToUnixMs(state.StartedAtUtc), (long)BombState.Duration.TotalMilliseconds,
        state.Resolved, state.Resolved ? state.Defused : null);

    private static long ToUnixMs(DateTime utc) => new DateTimeOffset(utc).ToUnixTimeMilliseconds();
}
