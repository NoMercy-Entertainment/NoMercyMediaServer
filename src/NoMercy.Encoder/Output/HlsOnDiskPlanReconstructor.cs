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
using NoMercy.Encoder.Bundle;
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
public partial class HlsOnDiskPlanReconstructor(
    IMediaAnalyzer mediaAnalyzer,
    IMediaBlueprintWriter? blueprintReader = null
)
{
    // MediaBlueprintWriter.ReadAsync has no dependency on the IMediaBlueprintBuilder
    // ctor param it carries (that's only used by WriteAsync) — building one locally
    // keeps this reconstructor self-contained instead of forcing every construction
    // site (this class is `new()`'d directly, never resolved through DI) to also
    // wire up the blueprint writer's dependency chain.
    private static readonly IMediaBlueprintWriter FallbackBlueprintReader =
        new MediaBlueprintWriter(builder: new MediaBlueprintBuilder());

    private IMediaBlueprintWriter BlueprintReader => blueprintReader ?? FallbackBlueprintReader;

    [GeneratedRegex(pattern: @"^video_(?<width>\d+)x(?<height>\d+)(?<sdr>_SDR)?$")]
    private static partial Regex VideoRenditionDirRegex();

    // The codec suffix is optional: older encodes published `audio_<lang>/`
    // with no codec token at all (the profile's naming template gained the
    // `_<codec>` suffix later), and this is the FALLBACK tier only reached
    // when no blueprint exists to read the resolved output paths from — see
    // ReconstructAudio. Requiring the suffix here silently dropped every
    // old-naming rendition from the rebuilt master, which is what shipped a
    // title with a missing audio group (plays silent).
    [GeneratedRegex(pattern: @"^audio_(?<lang>[a-zA-Z]{2,3})(?:_(?<codec>[a-zA-Z0-9]+))?(_\d+)?$")]
    private static partial Regex AudioRenditionDirRegex();

    private static readonly HashSet<string> HdrColorTransfers = new(
        comparer: StringComparer.OrdinalIgnoreCase
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
            storage: storage,
            outputDirectory: outputDirectory,
            ct: ct
        );

        // The blueprint is read once and shared: it is the naming-independent
        // source of truth for rendition discovery (the profile's naming
        // templates — PlaylistNameTemplate, TemplateResolver — are
        // user-customizable, so parsing a directory name is fundamentally
        // fragile). Video stays disk/probe-based even when a blueprint
        // exists: a stream-copied video BlueprintTrack carries no
        // width/height (only a transcoded track's OriginalParams does), so
        // the actual pixel dimensions still have to come from either the
        // `video_<w>x<h>` directory name or probing the file — this
        // reconstructor already does the latter as the authoritative
        // source and only falls back to the name for a failed probe.
        // Subtitles are matched purely by file extension under
        // `subtitles/{lang}/` (never a codec-suffixed name), so they were
        // never exposed to this bug class either.
        MediaBlueprint? blueprint = await BlueprintReader.ReadAsync(storage: storage, mediaRootPath: outputDirectory, ct: ct);
        IReadOnlyList<BlueprintTrack> blueprintTracks =
            blueprint?.Encodes.SelectMany(selector: encode => encode.Tracks).ToList() ?? [];

        return new OutputPlan(
            Format: OutputFormat.Hls,
            VideoOutputs: videoOutputs,
            AudioOutputs: ReconstructAudio(storage: storage, outputDirectory: outputDirectory, blueprintTracks: blueprintTracks),
            SubtitleOutputs: ReconstructSubtitles(storage: storage, outputDirectory: outputDirectory),
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

        foreach (string dirName in ListRenditionDirs(storage: storage, outputDirectory: outputDirectory, pattern: "video_*"))
        {
            Match match = VideoRenditionDirRegex().Match(input: dirName);
            if (!match.Success)
                continue;

            int dirWidth = int.Parse(s: match.Groups[groupname: "width"].Value);
            int dirHeight = int.Parse(s: match.Groups[groupname: "height"].Value);
            bool isSdrByName = match.Groups[groupname: "sdr"].Success;

            string dir = storage.CombinePath(parent: outputDirectory, child: dirName);
            (
                string? codec,
                int probedWidth,
                int probedHeight,
                bool tenBit,
                string? colorTransfer,
                double frameRate,
                bool isFmp4
            ) = await ProbeVideoAsync(storage: storage, dir: dir, ct: ct);

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
                ? !isSdrByName && HdrColorTransfers.Contains(item: colorTransfer)
                : !isSdrByName;

            outputs.Add(
                item: new VideoOutputPlan(
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

    /// <summary>
    /// PRIMARY: the encoder's own per-item <see cref="MediaBlueprint"/> when one
    /// is present. FALLBACK: a lenient parse of the on-disk <c>audio_*/</c>
    /// directory names, for encodes that predate the blueprint. The blueprint
    /// is authoritative because <see cref="BlueprintTrack.Files"/> are the
    /// RESOLVED output paths the encoder actually wrote — correct regardless
    /// of what naming template produced them — where a directory-name parse
    /// has to assume the template's shape and breaks the moment a custom
    /// profile (or an older encoder version) names things differently.
    /// </summary>
    private AudioOutputPlan[] ReconstructAudio(
        IStorage storage,
        string outputDirectory,
        IReadOnlyList<BlueprintTrack> blueprintTracks
    )
    {
        AudioOutputPlan[] fromBlueprint = ReconstructAudioFromBlueprint(blueprintTracks: blueprintTracks);
        return fromBlueprint.Length > 0
            ? fromBlueprint
            : ReconstructAudioFromDisk(storage: storage, outputDirectory: outputDirectory);
    }

    private static AudioOutputPlan[] ReconstructAudioFromBlueprint(
        IReadOnlyList<BlueprintTrack> blueprintTracks
    )
    {
        List<AudioOutputPlan> outputs = [];

        // Files.Count == 0 is a track the blueprint recorded but that never
        // produced a live artifact (dropped / superseded by a later preset
        // run) — correctly contributes nothing to the master.
        foreach (
            BlueprintTrack track in blueprintTracks.Where(predicate: track =>
                track is { Kind: "audio", Files.Count: > 0 }
            )
        )
        {
            string? playlistFile = track.Files.FirstOrDefault(predicate: file =>
                file.EndsWith(value: ".m3u8", comparisonType: StringComparison.OrdinalIgnoreCase)
            );
            if (playlistFile is null)
                continue;

            string playlistTemplate = playlistFile[..^".m3u8".Length];
            string language = string.IsNullOrWhiteSpace(value: track.SourceLanguage)
                ? "und"
                : track.SourceLanguage;
            string codec = ResolveAudioCodecToken(track: track, playlistFile: playlistFile);

            outputs.Add(
                item: new AudioOutputPlan(
                    EncoderName: codec,
                    BitrateKbps: 0,
                    Channels: 2,
                    SampleRate: 48000,
                    Action: StreamAction.Copy,
                    Language: language,
                    MapLabel: "0:a:0",
                    SegmentNameTemplate: playlistTemplate,
                    PlaylistNameTemplate: playlistTemplate,
                    SourceCodecName: codec
                )
            );
        }

        return outputs.ToArray();
    }

    /// <summary>
    /// The GROUP-ID naming token — purely a label distinguishing renditions in
    /// the master; playback always resolves through <see cref="BlueprintTrack.Files"/>,
    /// never this value. Parsed from the resolved rendition directory when it
    /// still follows the conventional <c>audio_&lt;lang&gt;_&lt;codec&gt;</c>
    /// shape, falling back to the blueprint's recorded source codec, then "aac".
    /// </summary>
    private static string ResolveAudioCodecToken(BlueprintTrack track, string playlistFile)
    {
        string? renditionDir = StoragePathHelpers.GetParent(path: playlistFile);
        string dirName = string.IsNullOrEmpty(value: renditionDir)
            ? string.Empty
            : StoragePathHelpers.GetName(path: renditionDir);

        Match match = AudioRenditionDirRegex().Match(input: dirName);
        if (match.Success && match.Groups[groupname: "codec"].Success)
            return match.Groups[groupname: "codec"].Value;

        return string.IsNullOrWhiteSpace(value: track.SourceCodec) ? "aac" : track.SourceCodec;
    }

    private static AudioOutputPlan[] ReconstructAudioFromDisk(
        IStorage storage,
        string outputDirectory
    )
    {
        List<AudioOutputPlan> outputs = [];

        foreach (string dirName in ListRenditionDirs(storage: storage, outputDirectory: outputDirectory, pattern: "audio_*"))
        {
            Match match = AudioRenditionDirRegex().Match(input: dirName);
            if (!match.Success)
                continue;

            string language = match.Groups[groupname: "lang"].Value;
            // Old-naming dirs (`audio_jpn`) carry no codec token — default to
            // aac, the group the master's video stream references by default
            // (PlaylistGenerator.GenerateMasterPlaylist's AUDIO="audio_aac").
            string codec = match.Groups[groupname: "codec"].Success ? match.Groups[groupname: "codec"].Value : "aac";

            outputs.Add(
                item: new AudioOutputPlan(
                    EncoderName: codec,
                    BitrateKbps: 0,
                    Channels: 2,
                    SampleRate: 48000,
                    Action: StreamAction.Copy,
                    Language: language,
                    MapLabel: "0:a:0",
                    SegmentNameTemplate: $"{dirName}/{dirName}",
                    PlaylistNameTemplate: $"{dirName}/{dirName}",
                    SourceCodecName: codec
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
        string subtitlesRoot = storage.CombinePath(parent: outputDirectory, child: "subtitles");
        if (!storage.Exists(path: subtitlesRoot))
            return [];

        foreach (StorageEntry languageEntry in storage.List(path: subtitlesRoot, pattern: "*", recursive: false))
        {
            if (!languageEntry.IsDirectory)
                continue;

            string language = storage.GetName(path: languageEntry.Path);

            foreach (
                StorageEntry trackEntry in storage.List(path: languageEntry.Path, pattern: "*", recursive: false)
            )
            {
                if (trackEntry.IsDirectory)
                    continue;

                string fileName = storage.GetName(path: trackEntry.Path);
                (SubtitleCodecType? codecType, string variant) = ClassifySubtitleFile(fileName: fileName);
                if (codecType is null)
                    continue;

                outputs.Add(
                    item: new SubtitleOutputPlan(
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
        if (fileName.EndsWith(value: ".m3u8", comparisonType: StringComparison.OrdinalIgnoreCase))
            return (SubtitleCodecType.WebVtt, fileName[..^".m3u8".Length]);
        if (fileName.EndsWith(value: ".ass", comparisonType: StringComparison.OrdinalIgnoreCase))
            return (SubtitleCodecType.Ass, fileName[..^".ass".Length]);
        if (fileName.EndsWith(value: ".srt", comparisonType: StringComparison.OrdinalIgnoreCase))
            return (SubtitleCodecType.Srt, fileName[..^".srt".Length]);

        return (null, string.Empty);
    }

    private static IEnumerable<string> ListRenditionDirs(
        IStorage storage,
        string outputDirectory,
        string pattern
    ) =>
        storage
            .List(path: outputDirectory, pattern: pattern, recursive: false)
            .Where(predicate: entry => entry.IsDirectory)
            .Select(selector: entry => storage.GetName(path: entry.Path));

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
        string initPath = storage.CombinePath(parent: dir, child: "init.mp4");
        bool isFmp4 = storage.Exists(path: initPath);
        string? probePath = isFmp4 ? initPath : FindFirstSegment(storage: storage, dir: dir);

        if (probePath is null)
            return (null, 0, 0, false, null, 0, isFmp4);

        try
        {
            MediaInfo info = await mediaAnalyzer.AnalyzeAsync(filePath: probePath, sourceStorage: storage, ct: ct);
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
            .List(path: dir, pattern: "*", recursive: false)
            .Where(predicate: entry =>
                !entry.IsDirectory
                && (
                    entry.Path.EndsWith(value: ".m4s", comparisonType: StringComparison.OrdinalIgnoreCase)
                    || entry.Path.EndsWith(value: ".ts", comparisonType: StringComparison.OrdinalIgnoreCase)
                )
            )
            .Select(selector: entry => entry.Path)
            .FirstOrDefault();
}
