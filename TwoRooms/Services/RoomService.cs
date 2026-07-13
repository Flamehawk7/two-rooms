using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using TwoRooms.Client.Game;
using TwoRooms.Hubs;

namespace TwoRooms.Services;

/// <summary>
/// Room Description Puzzle game logic, layered on top of the generic <see cref="Session"/>
/// pairing shell. The Outside seat (A) gets the true object layout but never the Inside seat's
/// arrangement; the Inside seat (B) gets their tray/arrangement but never the true layout.
/// </summary>
public sealed class RoomService(IHubContext<GameHub, IGameHubClient> hubContext)
{
    private readonly ConcurrentDictionary<string, RoomState> _states = new();

    private RoomState GetOrCreate(Session session)
    {
        return _states.GetOrAdd(session.Code, code =>
        {
            var state = new RoomState();
            lock (state.Lock)
            {
                state.Generate(SeededRng(code, attempt: 1));
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
            RoomLayoutUpdate view;
            lock (state.Lock)
            {
                view = new RoomLayoutUpdate(session.Code, state.Attempt, RoomState.Rows, RoomState.Cols, state.TrueObjects.Select(o => new PlacedObjectDto(o.Shape, o.Color, o.Row, o.Col)).ToList(), state.Solved);
            }
            await hubContext.Clients.Client(connectionId).OnRoomLayout(view);
        }
        else
        {
            RoomArrangementUpdate view;
            lock (state.Lock)
            {
                view = new RoomArrangementUpdate(session.Code, state.Attempt, RoomState.Rows, RoomState.Cols, state.Tray, state.PlacedObjects, state.CheckCount, state.Solved);
            }
            await hubContext.Clients.Client(connectionId).OnRoomArrangement(view);
        }
    }

    public async Task PlaceObject(Session session, SessionSeat seat, RoomShape shape, RoomColor color, int row, int col)
    {
        if (seat != SessionSeat.B) return; // only the Inside seat arranges

        var state = GetOrCreate(session);
        lock (state.Lock)
        {
            state.TryPlace(shape, color, row, col);
        }

        await SendStateToSeat(session, SessionSeat.B);
    }

    public async Task RemoveObject(Session session, SessionSeat seat, int row, int col)
    {
        if (seat != SessionSeat.B) return;

        var state = GetOrCreate(session);
        lock (state.Lock)
        {
            state.TryRemove(row, col);
        }

        await SendStateToSeat(session, SessionSeat.B);
    }

    public async Task CheckArrangement(Session session, SessionSeat seat)
    {
        if (seat != SessionSeat.B) return;

        var state = GetOrCreate(session);
        RoomCheckResultMessage checkResult;
        RoomCompletedMessage? completed = null;

        lock (state.Lock)
        {
            var (correct, total) = state.Check();
            checkResult = new RoomCheckResultMessage(session.Code, state.Attempt, correct, total, state.Solved);
            if (state.Solved)
            {
                completed = new RoomCompletedMessage(session.Code, state.Attempt, state.ElapsedMs, state.CheckCount, state.BestCompletionMs);
            }
        }

        await hubContext.Clients.Group(session.Code).OnRoomCheckResult(checkResult);
        if (completed is not null)
        {
            await hubContext.Clients.Group(session.Code).OnRoomCompleted(completed);
        }

        await SendStateToSeat(session, SessionSeat.B);
    }

    public async Task NewRoom(Session session)
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

        await SendStateToSeat(session, SessionSeat.A);
        await SendStateToSeat(session, SessionSeat.B);
    }

    /// <summary>Same session code + attempt always carves the same room, independent of any other game's RNG use.</summary>
    private static Random SeededRng(string sessionCode, int attempt) =>
        new(Session.StableHash($"{sessionCode}:room:{attempt}"));
}
