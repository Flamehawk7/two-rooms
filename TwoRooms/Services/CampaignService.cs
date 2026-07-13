using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using TwoRooms.Client.Game;
using TwoRooms.Hubs;

namespace TwoRooms.Services;

/// <summary>
/// Multi-Room Campaign orchestration: a packaging layer on top of the four standalone game
/// services, not a game in its own right. Activating a stage just calls that game's existing
/// "New X" method, which already generates a fresh puzzle and pushes role-filtered state to each
/// seat -- this layer only tracks which stage is active and advances when told to.
/// </summary>
public sealed class CampaignService(
    IHubContext<GameHub, IGameHubClient> hubContext,
    MazeService maze,
    SymbolLockService symbolLock,
    BombService bomb,
    RoomService room)
{
    private readonly ConcurrentDictionary<string, CampaignState> _states = new();

    private CampaignState GetOrCreate(string code) => _states.GetOrAdd(code, _ => new CampaignState());

    public async Task SendCurrentState(Session session)
    {
        await BroadcastState(session, GetOrCreate(session.Code));
    }

    public async Task StartCampaign(Session session)
    {
        var state = GetOrCreate(session.Code);
        int attempt;
        lock (state.Lock)
        {
            attempt = state.Attempt + 1;
        }
        lock (state.Lock)
        {
            state.Start(SeededRng(session.Code, attempt));
        }

        await BroadcastState(session, state);
        await ActivateCurrentStage(session, state);
    }

    public async Task AdvanceCampaign(Session session)
    {
        var state = GetOrCreate(session.Code);
        bool advanced;
        bool completed;
        lock (state.Lock)
        {
            advanced = state.Advance();
            completed = state.Completed;
        }

        if (!advanced) return;

        await BroadcastState(session, state);
        if (!completed)
        {
            await ActivateCurrentStage(session, state);
        }
    }

    private async Task ActivateCurrentStage(Session session, CampaignState state)
    {
        CampaignGameKind kind;
        lock (state.Lock)
        {
            kind = state.CurrentGame;
        }

        switch (kind)
        {
            case CampaignGameKind.Maze:
                await maze.NewMaze(session);
                break;
            case CampaignGameKind.SymbolLock:
                // MultiStage rather than Single, as a cheap way to satisfy "escalating difficulty".
                await symbolLock.NewLock(session, LockDifficulty.MultiStage);
                break;
            case CampaignGameKind.Bomb:
                await bomb.NewBomb(session);
                break;
            case CampaignGameKind.Room:
                await room.NewRoom(session);
                break;
        }
    }

    private async Task BroadcastState(Session session, CampaignState state)
    {
        CampaignStateUpdate update;
        lock (state.Lock)
        {
            update = new CampaignStateUpdate(
                session.Code, state.Attempt, state.StageOrder, state.CurrentStageIndex,
                state.Started, state.Completed, state.Started ? ToUnixMs(state.StartedAtUtc) : 0, state.CompletedElapsedMs, state.BestCompletionMs);
        }
        await hubContext.Clients.Group(session.Code).OnCampaignState(update);
    }

    private static Random SeededRng(string sessionCode, int attempt) =>
        new(Session.StableHash($"{sessionCode}:campaign:{attempt}"));

    private static long ToUnixMs(DateTime utc) => new DateTimeOffset(utc).ToUnixTimeMilliseconds();
}
