namespace NoMercy.Encoder.LiveTranscode;

public interface ILiveSessionTransport
{
    Task SendToClientAsync(string sessionId, object message, CancellationToken ct);
}
