using NoMercy.EncoderV2.Abstractions;
using NoMercy.EncoderV2.Containers;

namespace NoMercy.EncoderV2.Factories;

/// <summary>
/// Factory for creating container instances
/// </summary>
public sealed class ContainerFactory : IContainerFactory
{
    public IReadOnlyList<string> AvailableContainers =>
    [
        "hls", "mp4", "fmp4", "mkv", "matroska", "webm"
    ];

    public IContainer? CreateContainer(string formatName)
    {
        return formatName.ToLowerInvariant() switch
        {
            "hls" or "m3u8" => new HlsContainer(),
            "mp4" => new Mp4Container(),
            "fmp4" or "fragmented-mp4" => new FragmentedMp4Container(),
            "mkv" or "matroska" => new MkvContainer(),
            "webm" => new WebMContainer(),
            _ => null
        };
    }

    /// <summary>
    /// Creates an HLS container with specified settings
    /// </summary>
    public IHlsContainer CreateHlsContainer(int segmentDuration = 4, HlsPlaylistType playlistType = HlsPlaylistType.Vod)
    {
        return new HlsContainer
        {
            SegmentDuration = segmentDuration,
            PlaylistType = playlistType
        };
    }

    /// <summary>
    /// Creates an MP4 container with faststart enabled
    /// </summary>
    public IContainer CreateStreamableMp4()
    {
        return new Mp4Container
        {
            FastStart = true
        };
    }

    /// <summary>
    /// Creates a fragmented MP4 container for DASH
    /// </summary>
    public IContainer CreateDashCompatibleMp4(int fragmentDurationMs = 4000)
    {
        return new FragmentedMp4Container
        {
            FragmentDuration = fragmentDurationMs
        };
    }
}
