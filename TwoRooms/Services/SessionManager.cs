using System.Collections.Concurrent;

namespace TwoRooms.Services;

/// <summary>Singleton in-memory registry of active pairing sessions, keyed by session code.</summary>
public sealed class SessionManager
{
    private readonly ConcurrentDictionary<string, Session> _sessions = new();

    public Session GetOrCreate(string code) => _sessions.GetOrAdd(code, c => new Session(c));

    public Session? TryGet(string code) => _sessions.GetValueOrDefault(code);

    public void Remove(string code) => _sessions.TryRemove(code, out _);
}
