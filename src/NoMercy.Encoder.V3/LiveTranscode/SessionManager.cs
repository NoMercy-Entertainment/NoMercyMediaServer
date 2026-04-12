namespace NoMercy.Encoder.V3.LiveTranscode;

using System.Collections.Concurrent;

public class SessionManager(LiveSessionLimits limits) : ISessionManager
{
    private readonly ConcurrentDictionary<string, ILiveSession> _sessions = new();

    // Track which user owns each session — null key means anonymous
    private readonly ConcurrentDictionary<string, string> _sessionUserMap = new();

    public IReadOnlyList<ILiveSession> ActiveSessions => [.. _sessions.Values];

    public int ActiveSessionCount => _sessions.Count;

    public bool CanStartSession(string? userId = null)
    {
        if (_sessions.Count >= limits.MaxConcurrentSessions)
            return false;

        if (userId is not null)
        {
            int userCount = _sessionUserMap.Values.Count(uid => uid == userId);
            if (userCount >= limits.MaxSessionsPerUser)
                return false;
        }

        return true;
    }

    public void RegisterSession(ILiveSession session, string? userId = null)
    {
        _sessions[session.SessionId] = session;

        if (userId is not null)
            _sessionUserMap[session.SessionId] = userId;
    }

    public void RemoveSession(string sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
        _sessionUserMap.TryRemove(sessionId, out _);
    }
}
