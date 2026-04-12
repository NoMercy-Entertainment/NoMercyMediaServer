namespace NoMercy.Encoder.V3.LiveTranscode;

public interface ILiveEncoder
{
    Task<ILiveSession> StartAsync(LiveEncodeRequest request, CancellationToken ct);
}
