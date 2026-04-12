namespace NoMercy.Encoder.V3.LiveTranscode;

public interface ISessionManager
{
    IReadOnlyList<ILiveSession> ActiveSessions { get; }
    bool CanStartSession(string? userId = null);
    void RegisterSession(ILiveSession session, string? userId = null);
    void RemoveSession(string sessionId);
    int ActiveSessionCount { get; }
}
