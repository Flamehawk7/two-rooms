using TwoRooms.Client.Game;

namespace TwoRooms.Services;

/// <summary>
/// Generic, game-agnostic two-seat pairing session: who's connected, which seat they hold, and
/// the shared RNG. Game-specific state (round scores, maze layout, etc.) lives in its own
/// per-game service keyed by <see cref="Code"/>, not here, so multiple games can share this
/// session shell without tangling their state together.
/// </summary>
public sealed class Session(string code)
{
    private sealed class PlayerSlot
    {
        public required string ConnectionId;
        public required string Name;
        public required string Token;
        public bool Connected = true;
    }

    public string Code { get; } = code;
    public object Lock { get; } = new();

    /// <summary>Deterministic per-code seed so a session code reproduces the same random sequence.</summary>
    public Random Rng { get; } = new(StableHash(code));

    private PlayerSlot? _slotA;
    private PlayerSlot? _slotB;

    public bool BothConnected => _slotA is { Connected: true } && _slotB is { Connected: true };
    public bool IsEmpty => _slotA is null && _slotB is null;

    /// <summary>
    /// Assigns the caller to a seat. A matching <paramref name="token"/> reclaims that player's
    /// existing seat (reconnect after a refresh/drop), even mid-game. Otherwise falls back to
    /// the first free or vacated seat. Must be called under <see cref="Lock"/>.
    /// </summary>
    public SessionSeat? TryClaimSeat(string connectionId, string name, string token)
    {
        if (_slotA is not null && _slotA.Token == token)
        {
            Reclaim(_slotA, connectionId, name);
            return SessionSeat.A;
        }
        if (_slotB is not null && _slotB.Token == token)
        {
            Reclaim(_slotB, connectionId, name);
            return SessionSeat.B;
        }

        if (_slotA is null)
        {
            _slotA = new PlayerSlot { ConnectionId = connectionId, Name = name, Token = token };
            return SessionSeat.A;
        }
        if (!_slotA.Connected)
        {
            _slotA = new PlayerSlot { ConnectionId = connectionId, Name = name, Token = token };
            return SessionSeat.A;
        }

        if (_slotB is null)
        {
            _slotB = new PlayerSlot { ConnectionId = connectionId, Name = name, Token = token };
            return SessionSeat.B;
        }
        if (!_slotB.Connected)
        {
            _slotB = new PlayerSlot { ConnectionId = connectionId, Name = name, Token = token };
            return SessionSeat.B;
        }

        return null; // both seats occupied by live connections belonging to other players
    }

    public SessionSeat? SeatOf(string connectionId)
    {
        if (_slotA?.ConnectionId == connectionId) return SessionSeat.A;
        if (_slotB?.ConnectionId == connectionId) return SessionSeat.B;
        return null;
    }

    public string? ConnectionIdOf(SessionSeat seat) => seat == SessionSeat.A ? _slotA?.ConnectionId : _slotB?.ConnectionId;

    public void Disconnect(string connectionId)
    {
        if (_slotA?.ConnectionId == connectionId) _slotA.Connected = false;
        if (_slotB?.ConnectionId == connectionId) _slotB.Connected = false;
    }

    public SessionStateUpdate ToStateUpdate()
    {
        var players = new List<PlayerInfo>();
        if (_slotA is not null) players.Add(new PlayerInfo(_slotA.Name, SessionSeat.A, _slotA.Connected));
        if (_slotB is not null) players.Add(new PlayerInfo(_slotB.Name, SessionSeat.B, _slotB.Connected));

        return new SessionStateUpdate(Code, players, BothConnected);
    }

    private static void Reclaim(PlayerSlot slot, string connectionId, string name)
    {
        slot.ConnectionId = connectionId;
        slot.Name = name;
        slot.Connected = true;
    }

    /// <summary>Deterministic (non-cryptographic) string hash, stable across processes unlike string.GetHashCode.</summary>
    internal static int StableHash(string value)
    {
        unchecked
        {
            int hash = 17;
            foreach (var c in value)
            {
                hash = hash * 31 + c;
            }
            return hash;
        }
    }
}
