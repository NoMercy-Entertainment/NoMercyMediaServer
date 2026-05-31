using NoMercy.Encoder.Output;

namespace NoMercy.Encoder.BuildingBlocks;

public interface IHlsVariantAnalyzer
{
    HlsVariantAnalyzer.VariantMetrics Measure(string playlistPath);
}
