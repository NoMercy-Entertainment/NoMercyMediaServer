using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Encoder.Pipeline;

/// <summary>
/// Static helpers shared by pipeline stages that iterate a V2
/// <see cref="EncodingProfile"/>'s stream outputs.
/// </summary>
internal static class PlanStageHelpers
{
    /// <summary>
    /// Returns the effective set of <see cref="VideoOutput"/> entries for a
    /// V2 profile, expanding ladder rungs when the ladder is in Manual mode.
    ///
    /// <list type="bullet">
    ///   <item>No Video → empty array.</item>
    ///   <item>Ladder.Mode == Manual with rungs → each rung materialised to
    ///     a VideoOutput derived from the base Video output.</item>
    ///   <item>Otherwise → single-element array containing Video.</item>
    /// </list>
    ///
    /// Auto-ladder expansion (LadderMode.Auto) is handled by PlanStage via
    /// <see cref="IAbrLadderGenerator"/>, which produces the final
    /// VideoOutput[] and replaces the profile reference before this helper
    /// is called. So by the time <c>EnumerateVideo</c> runs on the expanded
    /// profile, the Ladder is already null or Manual with materialised rungs.
    /// </summary>
    internal static VideoOutput[] EnumerateVideo(EncodingProfile profile)
    {
        if (
            profile.Ladder?.Mode == LadderMode.Manual
            && profile.Ladder.Rungs is { Length: > 0 } rungs
        )
        {
            VideoOutput reference = profile.Video ?? BuildSyntheticReference(rungs[0]);
            return rungs.Select(r => RungToVideoOutput(r, reference)).ToArray();
        }

        if (profile.Video is null)
            return [];

        return [profile.Video];
    }

    /// <summary>
    /// When no <see cref="VideoOutput"/> reference is provided alongside a
    /// <see cref="LadderConfig"/>, synthesise a minimal reference from the
    /// first rung using safe defaults for fields the rung doesn't carry.
    /// </summary>
    internal static VideoOutput BuildSyntheticReference(LadderRung rung) =>
        new(
            Policy: StreamPolicy.Transcode,
            Codec: rung.Codec,
            Width: rung.Width,
            Height: rung.Height,
            RateControl: Profiles.RateControlMode.Crf,
            Crf: 23,
            BitrateKbps: rung.BitrateKbps,
            MaxBitrateKbps: rung.MaxBitrateKbps > 0 ? rung.MaxBitrateKbps : null,
            BufferSizeKbps: rung.BufferSizeKbps > 0 ? rung.BufferSizeKbps : null,
            Preset: rung.Preset,
            CodecProfile: rung.CodecProfile,
            Level: null,
            Tune: null,
            BitDepth: rung.BitDepth,
            PixelFormat: rung.PixelFormat,
            KeyframeIntervalSeconds: 2,
            ConvertHdrToSdr: false,
            SegmentNameTemplate: "video/{label}",
            PlaylistNameTemplate: "video/{label}/playlist"
        );

    /// <summary>Materialise one <see cref="LadderRung"/> into a full VideoOutput.</summary>
    internal static VideoOutput RungToVideoOutput(LadderRung rung, VideoOutput reference) =>
        reference with
        {
            Width = rung.Width,
            Height = rung.Height,
            Codec = rung.Codec,
            BitrateKbps = rung.BitrateKbps,
            MaxBitrateKbps = rung.MaxBitrateKbps,
            BufferSizeKbps = rung.BufferSizeKbps,
            BitDepth = rung.BitDepth,
            PixelFormat = rung.PixelFormat ?? reference.PixelFormat,
            Preset = rung.Preset ?? reference.Preset,
            CodecProfile = rung.CodecProfile,
        };

    /// <summary>
    /// Maps a V2 <see cref="Container"/> to the internal <see cref="OutputFormat"/>
    /// used by output strategies and <see cref="NoMercy.Encoder.Output.OutputPlan"/>.
    /// </summary>
    internal static OutputFormat ContainerToOutputFormat(Container container) =>
        container switch
        {
            Container.HlsTs => OutputFormat.Hls,
            Container.HlsFmp4 => OutputFormat.Hls,
            Container.AudioHlsTs => OutputFormat.AudioHls,
            Container.AudioHlsFmp4 => OutputFormat.AudioHls,
            Container.Mkv => OutputFormat.Mkv,
            Container.Mp4 => OutputFormat.Mp4,
            Container.Aac => OutputFormat.Mp4,
            Container.Dash => OutputFormat.Dash,
            Container.Mp3 => OutputFormat.Mp3,
            Container.Flac => OutputFormat.Flac,
            Container.Ogg => OutputFormat.Ogg,
            Container.Mka => OutputFormat.Mkv,
            Container.Mks => OutputFormat.Mkv,
            _ => OutputFormat.Hls,
        };
}
