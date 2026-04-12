namespace NoMercy.Encoder.V3.Analysis;

public interface IMediaAnalyzer
{
    Task<MediaInfo> AnalyzeAsync(string filePath, CancellationToken ct = default);
}
