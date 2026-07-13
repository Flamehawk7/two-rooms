namespace TwoRooms.Client.Game;

/// <summary>
/// Server-to-client callbacks for the game hub. Shared by the server (as the strongly-typed
/// Hub&lt;IGameHubClient&gt; contract) and the WASM client (as the set of .On handlers to register).
/// </summary>
public interface IGameHubClient
{
    Task OnSessionState(SessionStateUpdate update);
    Task OnDuelState(DuelStateUpdate update);
    Task OnRoundArmed();
    Task OnRoundSignal();
    Task OnRoundResult(RoundResultMessage result);
    Task OnMatchOver(MatchOverMessage result);
    Task OnMazeLayout(MazeLayoutUpdate update);
    Task OnMazePosition(MazePositionUpdate update);
    Task OnMazeCompleted(MazeCompletedMessage result);
    Task OnSymbolLockDoor(DoorViewUpdate update);
    Task OnSymbolLockCodex(CodexViewUpdate update);
    Task OnSymbolLockStageResult(SymbolLockStageResultMessage result);
    Task OnSymbolLockCompleted(SymbolLockCompletedMessage result);
    Task OnBombWires(WireDiagramUpdate update);
    Task OnBombManual(ManualUpdate update);
    Task OnBombResult(BombResultMessage result);
    Task OnRoomLayout(RoomLayoutUpdate update);
    Task OnRoomArrangement(RoomArrangementUpdate update);
    Task OnRoomCheckResult(RoomCheckResultMessage result);
    Task OnRoomCompleted(RoomCompletedMessage result);
    Task OnCampaignState(CampaignStateUpdate update);
    Task OnError(string message);
}
