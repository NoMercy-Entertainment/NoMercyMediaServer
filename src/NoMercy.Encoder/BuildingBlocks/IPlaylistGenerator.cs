namespace NoMercy.Encoder.BuildingBlocks;

using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;

public interface IPlaylistGenerator
{
    string GenerateMasterPlaylist(
        OutputPlan plan,
        string mediaTitle,
        Dictionary<string, HlsVariantAnalyzer.VariantMetrics> videoMetrics,
        Dictionary<string, HlsVariantAnalyzer.VariantMetrics> audioMetrics
    );
}
