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
public class GameHub(SessionManager sessions, ReactionDuelService reactionDuel, MazeService maze, SymbolLockService symbolLock, BombService bomb, RoomService room, CampaignService campaign) : Hub<IGameHubClient>
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
        await symbolLock.SendStateToSeat(session, seat.Value);
        await bomb.SendStateToSeat(session, seat.Value);
        await room.SendStateToSeat(session, seat.Value);
        await campaign.SendCurrentState(session);

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

    public async Task RequestSymbolLockState(string sessionCode)
    {
        var session = sessions.TryGet(NormalizeCode(sessionCode));
        if (session is null) return;

        var seat = session.SeatOf(Context.ConnectionId);
        if (seat is null) return;

        await symbolLock.SendStateToSeat(session, seat.Value);
    }

    public async Task SubmitSymbolLockCode(string sessionCode, string code)
    {
        var session = sessions.TryGet(NormalizeCode(sessionCode));
        if (session is null) return;

        var seat = session.SeatOf(Context.ConnectionId);
        if (seat is null) return;

        await symbolLock.SubmitCode(session, seat.Value, code);
    }

    public async Task AdvanceSymbolLockStage(string sessionCode)
    {
        var session = sessions.TryGet(NormalizeCode(sessionCode));
        if (session is null) return;
        await symbolLock.AdvanceStage(session);
    }

    public async Task NewSymbolLock(string sessionCode, LockDifficulty difficulty)
    {
        var session = sessions.TryGet(NormalizeCode(sessionCode));
        if (session is null) return;
        await symbolLock.NewLock(session, difficulty);
    }

    public async Task RequestBombState(string sessionCode)
    {
        var session = sessions.TryGet(NormalizeCode(sessionCode));
        if (session is null) return;

        var seat = session.SeatOf(Context.ConnectionId);
        if (seat is null) return;

        await bomb.SendStateToSeat(session, seat.Value);
    }

    public async Task CutWire(string sessionCode, int position)
    {
        var session = sessions.TryGet(NormalizeCode(sessionCode));
        if (session is null) return;

        var seat = session.SeatOf(Context.ConnectionId);
        if (seat is null) return;

        await bomb.CutWire(session, seat.Value, position);
    }

    public async Task NewBomb(string sessionCode)
    {
        var session = sessions.TryGet(NormalizeCode(sessionCode));
        if (session is null) return;
        await bomb.NewBomb(session);
    }

    public async Task RequestRoomState(string sessionCode)
    {
        var session = sessions.TryGet(NormalizeCode(sessionCode));
        if (session is null) return;

        var seat = session.SeatOf(Context.ConnectionId);
        if (seat is null) return;

        await room.SendStateToSeat(session, seat.Value);
    }

    public async Task PlaceRoomObject(string sessionCode, RoomShape shape, RoomColor color, int row, int col)
    {
        var session = sessions.TryGet(NormalizeCode(sessionCode));
        if (session is null) return;

        var seat = session.SeatOf(Context.ConnectionId);
        if (seat is null) return;

        await room.PlaceObject(session, seat.Value, shape, color, row, col);
    }

    public async Task RemoveRoomObject(string sessionCode, int row, int col)
    {
        var session = sessions.TryGet(NormalizeCode(sessionCode));
        if (session is null) return;

        var seat = session.SeatOf(Context.ConnectionId);
        if (seat is null) return;

        await room.RemoveObject(session, seat.Value, row, col);
    }

    public async Task CheckRoomArrangement(string sessionCode)
    {
        var session = sessions.TryGet(NormalizeCode(sessionCode));
        if (session is null) return;

        var seat = session.SeatOf(Context.ConnectionId);
        if (seat is null) return;

        await room.CheckArrangement(session, seat.Value);
    }

    public async Task NewRoom(string sessionCode)
    {
        var session = sessions.TryGet(NormalizeCode(sessionCode));
        if (session is null) return;
        await room.NewRoom(session);
    }

    public async Task StartCampaign(string sessionCode)
    {
        var session = sessions.TryGet(NormalizeCode(sessionCode));
        if (session is null) return;
        await campaign.StartCampaign(session);
    }

    public async Task AdvanceCampaign(string sessionCode)
    {
        var session = sessions.TryGet(NormalizeCode(sessionCode));
        if (session is null) return;
        await campaign.AdvanceCampaign(session);
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
