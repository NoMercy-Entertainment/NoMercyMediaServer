namespace NoMercy.Encoder.LiveTranscode;

public interface ILiveSessionTransport
{
    Task SendToClientAsync(string sessionId, object message, CancellationToken ct);
    Task<Stream> ServeSegmentAsync(string sessionId, int segmentIndex, CancellationToken ct);
}
