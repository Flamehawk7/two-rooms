using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using TwoRooms.Client.Game;
using TwoRooms.Hubs;

namespace TwoRooms.Services;

/// <summary>
/// Symbol-Matching Lock game logic, layered on top of the generic <see cref="Session"/> pairing
/// shell. The Reader (seat A) gets the door's symbol sequence but never the codex; the Codex
/// Keeper (seat B) gets the symbol-&gt;digit mapping but never the door sequence.
/// </summary>
public sealed class SymbolLockService(IHubContext<GameHub, IGameHubClient> hubContext)
{
    private readonly ConcurrentDictionary<string, SymbolLockState> _states = new();

    private SymbolLockState GetOrCreate(Session session)
    {
        return _states.GetOrAdd(session.Code, code =>
        {
            var state = new SymbolLockState();
            lock (state.Lock)
            {
                state.BeginAttempt(LockDifficulty.Single, SeededRng(code, attempt: 1, stage: 1));
            }
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
            DoorViewUpdate view;
            lock (state.Lock)
            {
                view = BuildDoorView(session.Code, state);
            }
            await hubContext.Clients.Client(connectionId).OnSymbolLockDoor(view);
        }
        else
        {
            CodexViewUpdate view;
            lock (state.Lock)
            {
                view = BuildCodexView(session.Code, state);
            }
            await hubContext.Clients.Client(connectionId).OnSymbolLockCodex(view);
        }
    }

    public async Task SubmitCode(Session session, SessionSeat seat, string code)
    {
        if (seat != SessionSeat.B) return; // only the Codex Keeper submits

        var state = GetOrCreate(session);
        SymbolLockStageResultMessage result;
        SymbolLockCompletedMessage? completed = null;

        lock (state.Lock)
        {
            var correct = state.TrySubmit(code.Trim());
            result = new SymbolLockStageResultMessage(
                session.Code, state.Stage, state.TotalStages, correct, state.StageMistakeCount, state.AllStagesSolved);

            if (state.AllStagesSolved)
            {
                completed = new SymbolLockCompletedMessage(
                    session.Code, state.Difficulty, state.TotalStages, state.TotalElapsedMs,
                    state.TotalMistakeCount, state.BestCompletionMs);
            }
        }

        await hubContext.Clients.Group(session.Code).OnSymbolLockStageResult(result);
        if (completed is not null)
        {
            await hubContext.Clients.Group(session.Code).OnSymbolLockCompleted(completed);
        }

        // The Codex Keeper's mistake/solved counters changed; refresh their view. The Reader's
        // door view is unaffected by a wrong guess and only needs refreshing on stage advance.
        await SendStateToSeat(session, SessionSeat.B);
    }

    public async Task AdvanceStage(Session session)
    {
        var state = GetOrCreate(session);
        lock (state.Lock)
        {
            if (!state.StageSolved || state.AllStagesSolved) return;
            state.AdvanceStage(SeededRng(session.Code, state.Attempt, state.Stage + 1));
        }

        await SendStateToSeat(session, SessionSeat.A);
        await SendStateToSeat(session, SessionSeat.B);
    }

    public async Task NewLock(Session session, LockDifficulty difficulty)
    {
        var state = GetOrCreate(session);
        int attempt;
        lock (state.Lock)
        {
            attempt = state.Attempt + 1;
        }
        lock (state.Lock)
        {
            state.BeginAttempt(difficulty, SeededRng(session.Code, attempt, stage: 1));
        }

        await SendStateToSeat(session, SessionSeat.A);
        await SendStateToSeat(session, SessionSeat.B);
    }

    /// <summary>Same session code + attempt + stage always carves the same puzzle, independent of any other game's RNG use.</summary>
    private static Random SeededRng(string sessionCode, int attempt, int stage) =>
        new(Session.StableHash($"{sessionCode}:lock:{attempt}:{stage}"));

    private static DoorViewUpdate BuildDoorView(string code, SymbolLockState state) => new(
        code, state.Difficulty, state.Stage, state.TotalStages,
        state.DoorSequence.Select(s => s.ToString()).ToList(), state.StageSolved, state.AllStagesSolved);

    private static CodexViewUpdate BuildCodexView(string code, SymbolLockState state) => new(
        code, state.Difficulty, state.Stage, state.TotalStages,
        state.Pool.Select(s => new SymbolCodexEntry(s.ToString(), state.Codex[s])).ToList(),
        state.StageMistakeCount, state.TotalMistakeCount, state.StageSolved, state.AllStagesSolved);
}
