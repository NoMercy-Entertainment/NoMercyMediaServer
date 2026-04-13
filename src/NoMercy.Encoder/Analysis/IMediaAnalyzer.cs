namespace NoMercy.Encoder.Analysis;

public interface IMediaAnalyzer
{
    Task<MediaInfo> AnalyzeAsync(string filePath, CancellationToken ct = default);
}
