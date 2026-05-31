namespace NoMercy.Encoder.Subtitles;

public interface ISubtitleAcquisitionService
{
    Task<IReadOnlyList<AcquiredSubtitle>> AcquireAsync(
        AcquisitionRequest request,
        CancellationToken ct
    );
}
