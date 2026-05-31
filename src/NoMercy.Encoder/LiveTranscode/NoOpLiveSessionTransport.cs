namespace NoMercy.Encoder.LiveTranscode;

public sealed class NoOpLiveSessionTransport : ILiveSessionTransport
{
    public Task SendToClientAsync(string sessionId, object message, CancellationToken ct) =>
        Task.CompletedTask;
}
