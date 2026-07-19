// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Text.RegularExpressions;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Storage;

namespace NoMercy.Encoder.Output;

/// <summary>
/// Rebuilds a minimal but complete <see cref="OutputPlan"/> purely from what's
/// actually published under an HLS output directory — <c>video_*/</c>,
/// <c>audio_*/</c>, and <c>subtitles/*/</c> — independent of any encode plan
/// held in memory.
///
/// A title encoded as a sequential cascade of independent preset bundles (or
/// a single preset the decode-aware bundler split into several
/// self-finalizing <c>Whole</c> bundles) has each bundle publish against only
/// its own slice of the ladder. A plan re-derived from a profile — even a
/// freshly re-planned "merged" one — reflects what the LAST bundle's request
/// describes, not what every bundle actually published; only the filesystem
/// itself is the union. Feeding the plan this reconstructs into
/// <see cref="HlsOutputStrategy.FinalizeAsync"/> makes the master a function
/// of on-disk reality, regardless of how many bundles contributed to it or
/// how many presets were involved.
/// </summary>
public partial class HlsOnDiskPlanReconstructor(IMediaAnalyzer mediaAnalyzer)
{
    [GeneratedRegex(@"^video_(?<width>\d+)x(?<height>\d+)(?<sdr>_SDR)?$")]
    private static partial Regex VideoRenditionDirRegex();

    [GeneratedRegex(@"^audio_(?<lang>[a-zA-Z]{2,3})_(?<codec>[a-zA-Z0-9]+)(_\d+)?$")]
    private static partial Regex AudioRenditionDirRegex();

    private static readonly HashSet<string> HdrColorTransfers = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "smpte2084",
        "arib-std-b67",
    };

    public async Task<OutputPlan> ReconstructAsync(
        IStorage storage,
        string outputDirectory,
        CancellationToken ct
    )
    {
        (VideoOutputPlan[] videoOutputs, bool anyFmp4) = await ReconstructVideoAsync(
            storage,
            outputDirectory,
            ct
        );

        return new OutputPlan(
            Format: OutputFormat.Hls,
            VideoOutputs: videoOutputs,
            AudioOutputs: ReconstructAudio(storage, outputDirectory),
            SubtitleOutputs: ReconstructSubtitles(storage, outputDirectory),
            Thumbnails: null,
            HlsOptions: new HlsPlanOptions(SegmentType: anyFmp4 ? "fmp4" : "mpegts"),
            // Advertise the WebVTT subtitle renditions in the master. The master
            // generator drops WebVTT subs entirely unless this is true (they are
            // assumed un-chunked and unadvertisable otherwise), which silently
            // stripped every subtitle from a reconstructed master. The chunks
            // already exist on disk, so WriteSubtitleSidecarsAsync no-ops on each
            // one whose playlist is already present (idempotent) — this stays a
            // pure read while still surfacing the subtitles.
            EmitSubtitleWebVttChunks: true
        );
    }

    private async Task<(VideoOutputPlan[] Outputs, bool AnyFmp4)> ReconstructVideoAsync(
        IStorage storage,
        string outputDirectory,
        CancellationToken ct
    )
    {
        List<VideoOutputPlan> outputs = [];
        bool anyFmp4 = false;

        foreach (string dirName in ListRenditionDirs(storage, outputDirectory, "video_*"))
        {
            Match match = VideoRenditionDirRegex().Match(dirName);
            if (!match.Success)
                continue;

            int dirWidth = int.Parse(match.Groups["width"].Value);
            int dirHeight = int.Parse(match.Groups["height"].Value);
            bool isSdrByName = match.Groups["sdr"].Success;

            string dir = storage.CombinePath(outputDirectory, dirName);
            (
                string? codec,
                int probedWidth,
                int probedHeight,
                bool tenBit,
                string? colorTransfer,
                double frameRate,
                bool isFmp4
            ) = await ProbeVideoAsync(storage, dir, ct);

            if (isFmp4)
                anyFmp4 = true;

            int width = probedWidth > 0 ? probedWidth : dirWidth;
            int height = probedHeight > 0 ? probedHeight : dirHeight;

            // Both signals must agree for HDR: the directory-name suffix
            // (authoritative — TemplateResolver.VideoTokens always suffixes a
            // tonemapped-to-SDR rendition with "_SDR") and, when a probe
            // succeeded, the stream's own transfer characteristic. A failed
            // probe falls back to the directory name alone rather than
            // defaulting to SDR — a probe hiccup must never mislabel a real
            // HDR rendition and strip its PQ signaling from the master.
            bool isHdrOutput = colorTransfer is not null
                ? !isSdrByName && HdrColorTransfers.Contains(colorTransfer)
                : !isSdrByName;

            outputs.Add(
                new VideoOutputPlan(
                    Width: width,
                    Height: height,
                    EncoderName: codec ?? "hevc",
                    Crf: 0,
                    BitrateKbps: 0,
                    Preset: null,
                    Profile: null,
                    Level: null,
                    TenBit: tenBit,
                    PixelFormat: tenBit ? "yuv420p10le" : "yuv420p",
                    MapLabel: "[v0]",
                    ExtraFlags: [],
                    FrameRate: frameRate > 0 ? frameRate : 23.976,
                    SegmentNameTemplate: $"{dirName}/{dirName}",
                    PlaylistNameTemplate: $"{dirName}/{dirName}",
                    IsHdrOutput: isHdrOutput
                )
            );
        }

        return (outputs.ToArray(), anyFmp4);
    }

    private AudioOutputPlan[] ReconstructAudio(IStorage storage, string outputDirectory)
    {
        List<AudioOutputPlan> outputs = [];

        foreach (string dirName in ListRenditionDirs(storage, outputDirectory, "audio_*"))
        {
            Match match = AudioRenditionDirRegex().Match(dirName);
            if (!match.Success)
                continue;

            string language = match.Groups["lang"].Value;
            string codec = match.Groups["codec"].Value;

            outputs.Add(
                new AudioOutputPlan(
                    EncoderName: codec,
                    BitrateKbps: 0,
                    Channels: 2,
                    SampleRate: 48000,
                    Action: StreamAction.Copy,
                    Language: language,
                    MapLabel: "0:a:0",
                    SegmentNameTemplate: $"{dirName}/{dirName}",
                    PlaylistNameTemplate: $"{dirName}/{dirName}"
                )
            );
        }

        return outputs.ToArray();
    }

    private static SubtitleOutputPlan[] ReconstructSubtitles(
        IStorage storage,
        string outputDirectory
    )
    {
        List<SubtitleOutputPlan> outputs = [];
        string subtitlesRoot = storage.CombinePath(outputDirectory, "subtitles");
        if (!storage.Exists(subtitlesRoot))
            return [];

        foreach (StorageEntry languageEntry in storage.List(subtitlesRoot, "*", recursive: false))
        {
            if (!languageEntry.IsDirectory)
                continue;

            string language = storage.GetName(languageEntry.Path);

            foreach (
                StorageEntry trackEntry in storage.List(languageEntry.Path, "*", recursive: false)
            )
            {
                if (trackEntry.IsDirectory)
                    continue;

                string fileName = storage.GetName(trackEntry.Path);
                (SubtitleCodecType? codecType, string variant) = ClassifySubtitleFile(fileName);
                if (codecType is null)
                    continue;

                outputs.Add(
                    new SubtitleOutputPlan(
                        OutputCodec: codecType.Value,
                        Action: StreamAction.Extract,
                        Language: language,
                        SourceIndex: 0,
                        MapLabel: null,
                        Variant: variant
                    )
                );
            }
        }

        return outputs.ToArray();
    }

    private static (SubtitleCodecType? CodecType, string Variant) ClassifySubtitleFile(
        string fileName
    )
    {
        if (fileName.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
            return (SubtitleCodecType.WebVtt, fileName[..^".m3u8".Length]);
        if (fileName.EndsWith(".ass", StringComparison.OrdinalIgnoreCase))
            return (SubtitleCodecType.Ass, fileName[..^".ass".Length]);
        if (fileName.EndsWith(".srt", StringComparison.OrdinalIgnoreCase))
            return (SubtitleCodecType.Srt, fileName[..^".srt".Length]);

        return (null, string.Empty);
    }

    private static IEnumerable<string> ListRenditionDirs(
        IStorage storage,
        string outputDirectory,
        string pattern
    ) =>
        storage
            .List(outputDirectory, pattern, recursive: false)
            .Where(entry => entry.IsDirectory)
            .Select(entry => storage.GetName(entry.Path));

    private async Task<(
        string? Codec,
        int Width,
        int Height,
        bool TenBit,
        string? ColorTransfer,
        double FrameRate,
        bool IsFmp4
    )> ProbeVideoAsync(IStorage storage, string dir, CancellationToken ct)
    {
        string initPath = storage.CombinePath(dir, "init.mp4");
        bool isFmp4 = storage.Exists(initPath);
        string? probePath = isFmp4 ? initPath : FindFirstSegment(storage, dir);

        if (probePath is null)
            return (null, 0, 0, false, null, 0, isFmp4);

        try
        {
            MediaInfo info = await mediaAnalyzer.AnalyzeAsync(probePath, storage, ct);
            VideoStreamInfo? stream = info.VideoStreams.FirstOrDefault();
            return stream is null
                ? (null, 0, 0, false, null, 0, isFmp4)
                : (
                    stream.Codec,
                    stream.Width,
                    stream.Height,
                    stream.BitDepth >= 10,
                    stream.ColorTransfer,
                    stream.FrameRate,
                    isFmp4
                );
        }
        catch (Exception)
        {
            // A corrupt/missing init segment must not sink the whole
            // reconstruction — the caller falls back to the directory-name
            // width/height/HDR signal for this one rendition.
            return (null, 0, 0, false, null, 0, isFmp4);
        }
    }

    private static string? FindFirstSegment(IStorage storage, string dir) =>
        storage
            .List(dir, "*", recursive: false)
            .Where(entry =>
                !entry.IsDirectory
                && (
                    entry.Path.EndsWith(".m4s", StringComparison.OrdinalIgnoreCase)
                    || entry.Path.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)
                )
            )
            .Select(entry => entry.Path)
            .FirstOrDefault();
}
