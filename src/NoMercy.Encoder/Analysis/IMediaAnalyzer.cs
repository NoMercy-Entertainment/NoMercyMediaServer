using NoMercy.Storage;

namespace NoMercy.Encoder.Analysis;

public interface IMediaAnalyzer
{
    Task<MediaInfo> AnalyzeAsync(string filePath, CancellationToken ct = default);

    Task<MediaInfo> AnalyzeAsync(
        string filePath,
        IStorage sourceStorage,
        CancellationToken ct = default
    );
}
