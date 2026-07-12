using Microsoft.AspNetCore.SignalR;
using TwoRooms.Client.Game;
using TwoRooms.Services;

namespace TwoRooms.Hubs;

/// <summary>
/// Generic real-time hub: session pairing (join, seat assignment, disconnect handling) plus thin
/// pass-throughs into per-game services. Game-specific rules live in those services (e.g.
/// <see cref="ReactionDuelService"/>), not here, so a second game can plug into the same session
/// shell without this class growing without bound.
/// </summary>
public class GameHub(SessionManager sessions, ReactionDuelService reactionDuel, MazeService maze) : Hub<IGameHubClient>
{
    private const string SessionCodeItemKey = "sessionCode";

    public async Task<JoinSessionResult> JoinSession(string sessionCode, string playerName, string playerToken)
    {
        var code = NormalizeCode(sessionCode);
        if (string.IsNullOrWhiteSpace(code))
        {
            return new JoinSessionResult(false, "Enter a session code.", default, code);
        }
        if (string.IsNullOrWhiteSpace(playerToken))
        {
            return new JoinSessionResult(false, "Missing player identity.", default, code);
        }

        var name = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
        if (name.Length > 24) name = name[..24];

        var session = sessions.GetOrCreate(code);
        SessionSeat? seat;
        lock (session.Lock)
        {
            seat = session.TryClaimSeat(Context.ConnectionId, name, playerToken);
        }

        if (seat is null)
        {
            return new JoinSessionResult(false, "That session already has two players.", default, code);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, code);
        Context.Items[SessionCodeItemKey] = code;

        await BroadcastState(session);
        await reactionDuel.SendCurrentState(session);
        await maze.SendStateToSeat(session, seat.Value);

        return new JoinSessionResult(true, null, seat.Value, code);
    }

    public async Task StartRound(string sessionCode)
    {
        var session = sessions.TryGet(NormalizeCode(sessionCode));
        if (session is null) return;
        await reactionDuel.StartRound(session);
    }

    public async Task React(string sessionCode)
    {
        var session = sessions.TryGet(NormalizeCode(sessionCode));
        if (session is null) return;

        var seat = session.SeatOf(Context.ConnectionId);
        if (seat is null) return;

        await reactionDuel.React(session, seat.Value);
    }

    public async Task RequestMazeState(string sessionCode)
    {
        var session = sessions.TryGet(NormalizeCode(sessionCode));
        if (session is null) return;

        var seat = session.SeatOf(Context.ConnectionId);
        if (seat is null) return;

        await maze.SendStateToSeat(session, seat.Value);
    }

    public async Task MazeMove(string sessionCode, MazeDirection direction)
    {
        var session = sessions.TryGet(NormalizeCode(sessionCode));
        if (session is null) return;

        var seat = session.SeatOf(Context.ConnectionId);
        if (seat is null) return;

        await maze.Move(session, seat.Value, direction);
    }

    public async Task NewMaze(string sessionCode)
    {
        var session = sessions.TryGet(NormalizeCode(sessionCode));
        if (session is null) return;
        await maze.NewMaze(session);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue(SessionCodeItemKey, out var codeObj) && codeObj is string code)
        {
            var session = sessions.TryGet(code);
            if (session is not null)
            {
                bool empty;
                lock (session.Lock)
                {
                    session.Disconnect(Context.ConnectionId);
                    empty = session.IsEmpty;
                }

                reactionDuel.OnPlayerDisconnected(session);

                if (empty)
                {
                    sessions.Remove(code);
                }
                else
                {
                    await BroadcastState(session);
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task BroadcastState(Session session)
    {
        SessionStateUpdate update;
        lock (session.Lock)
        {
            update = session.ToStateUpdate();
        }
        await Clients.Group(session.Code).OnSessionState(update);
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
}
