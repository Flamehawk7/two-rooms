using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using TwoRooms.Client.Game;
using TwoRooms.Hubs;

namespace TwoRooms.Services;

/// <summary>
/// Maze Navigator game logic, layered on top of the generic <see cref="Session"/> pairing shell.
/// This is the first game in the project that actually needs asymmetric, role-filtered pushes:
/// the Guide (seat A) gets the full layout but never the Navigator's position; the Navigator
/// (seat B) gets their position and local surroundings but never the layout.
/// </summary>
public sealed class MazeService(IHubContext<GameHub, IGameHubClient> hubContext)
{
    private const int Width = 12;
    private const int Height = 9;

    private readonly ConcurrentDictionary<string, MazeState> _states = new();

    private MazeState GetOrCreate(Session session)
    {
        return _states.GetOrAdd(session.Code, code =>
        {
            var state = new MazeState(Width, Height);
            lock (state.Lock)
            {
                state.Generate(SeededRng(code, attempt: 1));
            }
            return state;
        });
    }

    /// <summary>Pushes the current role-appropriate view to one seat (used on join/reconnect and after moves).</summary>
    public async Task SendStateToSeat(Session session, SessionSeat seat)
    {
        var connectionId = session.ConnectionIdOf(seat);
        if (connectionId is null) return;

        var state = GetOrCreate(session);
        if (seat == SessionSeat.A)
        {
            MazeLayoutUpdate layout;
            lock (state.Lock)
            {
                layout = BuildLayout(session.Code, state);
            }
            await hubContext.Clients.Client(connectionId).OnMazeLayout(layout);
        }
        else
        {
            MazePositionUpdate position;
            lock (state.Lock)
            {
                position = BuildPosition(session.Code, state);
            }
            await hubContext.Clients.Client(connectionId).OnMazePosition(position);
        }
    }

    public async Task Move(Session session, SessionSeat seat, MazeDirection direction)
    {
        if (seat != SessionSeat.B) return; // only the Navigator moves

        var state = GetOrCreate(session);
        MazePositionUpdate position;
        MazeCompletedMessage? completed = null;

        lock (state.Lock)
        {
            state.TryMove(direction);
            position = BuildPosition(session.Code, state);
            if (state.Solved)
            {
                completed = new MazeCompletedMessage(
                    session.Code, state.Attempt, state.ElapsedMs, state.MoveCount, state.BumpCount, state.BestCompletionMs);
            }
        }

        var connectionId = session.ConnectionIdOf(SessionSeat.B);
        if (connectionId is not null)
        {
            await hubContext.Clients.Client(connectionId).OnMazePosition(position);
        }

        if (completed is not null)
        {
            await hubContext.Clients.Group(session.Code).OnMazeCompleted(completed);
        }
    }

    public async Task NewMaze(Session session)
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

    /// <summary>Same session code + attempt number always carves the same maze, independent of any other game's RNG use.</summary>
    private static Random SeededRng(string sessionCode, int attempt) => new(Session.StableHash($"{sessionCode}:maze:{attempt}"));

    private static MazeLayoutUpdate BuildLayout(string code, MazeState state)
    {
        var cells = new List<MazeCellDto>(state.Width * state.Height);
        for (var y = 0; y < state.Height; y++)
        for (var x = 0; x < state.Width; x++)
        {
            var c = state.Cells[x, y];
            cells.Add(new MazeCellDto(c.North, c.East, c.South, c.West));
        }

        return new MazeLayoutUpdate(
            code, state.Attempt, state.Width, state.Height, cells,
            state.Start.X, state.Start.Y, state.Exit.X, state.Exit.Y, state.Solved);
    }

    private static MazePositionUpdate BuildPosition(string code, MazeState state)
    {
        var c = state.CurrentCell;
        return new MazePositionUpdate(
            code, state.Attempt, state.PlayerPos.X, state.PlayerPos.Y,
            new MazeCellDto(c.North, c.East, c.South, c.West), state.MoveCount, state.BumpCount, state.Solved);
    }
}
