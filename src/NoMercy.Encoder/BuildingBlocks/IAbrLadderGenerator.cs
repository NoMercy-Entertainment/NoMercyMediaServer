using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Encoder.BuildingBlocks;

/// <summary>
/// Generates an adaptive-bitrate quality ladder from source media.
/// </summary>
public interface IAbrLadderGenerator
{
    /// <summary>
    /// Legacy path: generates <see cref="VideoOutput"/> rungs using hardcoded
    /// tier bitrates scaled by source complexity.
    /// </summary>
    VideoOutput[] Generate(MediaInfo media, VideoOutput reference);

    /// <summary>
    /// New path: generates <see cref="LadderRung"/> rungs by reading every
    /// parameter from <paramref name="autoConfig"/> — no hardcoded constants.
    /// </summary>
    LadderRung[] GenerateLadder(
        MediaInfo media,
        VideoCodecType profileCodec,
        AutoLadderConfig autoConfig
    );
}
