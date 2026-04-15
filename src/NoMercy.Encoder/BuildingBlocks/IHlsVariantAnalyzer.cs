namespace NoMercy.Encoder.BuildingBlocks;

using NoMercy.Encoder.Output;

public interface IHlsVariantAnalyzer
{
    HlsVariantAnalyzer.VariantMetrics Measure(string playlistPath);
}
