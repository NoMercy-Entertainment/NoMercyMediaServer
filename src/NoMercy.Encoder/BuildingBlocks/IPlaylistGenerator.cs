using NoMercy.Encoder.Output;

namespace NoMercy.Encoder.BuildingBlocks;

public interface IPlaylistGenerator
{
    string GenerateMasterPlaylist(
        OutputPlan plan,
        string mediaTitle,
        Dictionary<string, HlsVariantAnalyzer.VariantMetrics> videoMetrics,
        Dictionary<string, HlsVariantAnalyzer.VariantMetrics> audioMetrics
    );
}
