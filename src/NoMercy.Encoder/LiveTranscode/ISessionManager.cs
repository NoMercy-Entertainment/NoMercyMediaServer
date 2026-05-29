namespace NoMercy.Encoder.LiveTranscode;

public interface ISessionManager
{
    IReadOnlyList<ILiveSession> ActiveSessions { get; }
    bool CanStartSession(string? userId = null);
    void RegisterSession(ILiveSession session, string? userId = null);
    void RemoveSession(string sessionId);
    int ActiveSessionCount { get; }

    /// <summary>
    /// Returns the user id that owns <paramref name="sessionId"/>, or null when
    /// the session was registered anonymously or does not exist.
    /// </summary>
    string? GetOwnerUserId(string sessionId);
}
