namespace NoMercy.Encoder.LiveTranscode;

public interface ILiveEncoder
{
    Task<ILiveSession> StartAsync(LiveEncodeRequest request, CancellationToken ct);
}
