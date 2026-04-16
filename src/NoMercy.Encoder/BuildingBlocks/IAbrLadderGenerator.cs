namespace NoMercy.Encoder.BuildingBlocks;

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Profiles;

/// <summary>
/// Generates an adaptive-bitrate quality ladder from a single reference video
/// profile and the source media. Tiers above the source resolution are skipped
/// so a 720p source never gets a 1080p output.
/// </summary>
public interface IAbrLadderGenerator
{
    VideoOutput[] Generate(MediaInfo media, VideoOutput reference);
}
