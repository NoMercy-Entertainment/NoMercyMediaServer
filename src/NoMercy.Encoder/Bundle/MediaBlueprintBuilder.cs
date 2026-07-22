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

using Newtonsoft.Json.Linq;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Naming;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Subtitles;

namespace NoMercy.Encoder.Bundle;

/// <inheritdoc cref="IMediaBlueprintBuilder"/>
public class MediaBlueprintBuilder : IMediaBlueprintBuilder
{
    private const int CurrentVersion = 1;

    public MediaBlueprint BuildFromSource(
        MediaInfo source,
        BlueprintIdentity identity,
        string? originalSourcePath = null
    )
    {
        // MediaInfo.FilePath is the staging lease the analyzer was pointed at,
        // which is released the moment the encode ends. Recording it would
        // describe the source by a path that no longer exists — the one thing a
        // reconstruction blueprint must never do. OriginalInputPath carries the
        // real source, so prefer it and fall back only when no caller threaded it.
        string sourcePath = originalSourcePath ?? source.FilePath;

        BlueprintSource blueprintSource = new(
            Path: sourcePath,
            Filename: Path.GetFileName(path: sourcePath),
            Container: source.Format,
            SizeBytes: source.FileSizeBytes,
            DurationSeconds: source.Duration.TotalSeconds,
            // Deferred until a streaming hasher is wired into the analyzer —
            // see spec "Open items".
            Sha256: null,
            Ffprobe: source.Ffprobe
        );

        return new(
            Version: CurrentVersion,
            Identity: identity,
            Source: blueprintSource,
            // Zero encode entries: this builder runs source-derived only, no
            // encode has happened yet. Proves the blueprint is generatable
            // with zero encode outputs — the foundation-slice invariant.
            Encodes: []
        );
    }

    public BlueprintEncode BuildEncode(
        MediaInfo source,
        OutputPlan plan,
        BundleLayout layout,
        IReadOnlyList<string> outputFiles,
        string outputLocation,
        string encoderVersion,
        string? profileFingerprint,
        DateTime createdAt,
        DateTime completedAt
    )
    {
        List<BlueprintTrack> tracks =
        [
            .. BuildVideoTracks(streams: source.VideoStreams, outputs: plan.VideoOutputs, layout: layout, outputFiles: outputFiles),
            .. BuildAudioTracks(streams: source.AudioStreams, outputs: plan.AudioOutputs, layout: layout, outputFiles: outputFiles),
            .. BuildSubtitleTracks(
                streams: source.SubtitleStreams,
                outputs: plan.SubtitleOutputs,
                layout: layout,
                outputFiles: outputFiles
            ),
        ];

        string targetContainer = SourceContainerMapper.Map(formatName: source.Format);

        return new(
            PresetSlug: layout.PresetSlug,
            PresetId: layout.PresetId,
            ProfileFingerprint: profileFingerprint,
            EncoderVersion: encoderVersion,
            TargetContainer: targetContainer,
            OutputLocation: outputLocation,
            CreatedAt: createdAt,
            CompletedAt: completedAt,
            Tracks: tracks,
            ReconstructionCommandTemplate: BuildCommandTemplate(targetContainer: targetContainer, tracks: tracks),
            LossyWarnings: BuildWarnings(tracks: tracks)
        );
    }

    // -----------------------------------------------------------------------
    // Video — walk source.VideoStreams, never plan.VideoOutputs
    // -----------------------------------------------------------------------

    private static List<BlueprintTrack> BuildVideoTracks(
        IReadOnlyList<VideoStreamInfo> streams,
        VideoOutputPlan[] outputs,
        BundleLayout layout,
        IReadOnlyList<string> outputFiles
    )
    {
        List<BlueprintTrack> tracks = [];
        foreach (VideoStreamInfo stream in streams)
        {
            List<VideoOutputPlan> matched = outputs
                .Where(predicate: o => o.SourceStreamIndex == stream.Index)
                .ToList();
            tracks.Add(item: BuildVideoTrack(stream: stream, matched: matched, layout: layout, outputFiles: outputFiles));
        }
        return tracks;
    }

    private static BlueprintTrack BuildVideoTrack(
        VideoStreamInfo stream,
        List<VideoOutputPlan> matched,
        BundleLayout layout,
        IReadOnlyList<string> outputFiles
    )
    {
        // Highest fidelity wins: a stream-copied rendition beats every
        // transcoded rung the same ladder also produced for this stream.
        VideoOutputPlan? copyOutput = matched.FirstOrDefault(predicate: o =>
            string.Equals(a: o.EncoderName, b: "copy", comparisonType: StringComparison.OrdinalIgnoreCase)
        );

        if (copyOutput is not null)
            return new(
                SourceStreamIndex: stream.Index,
                Kind: "video",
                SourceCodec: stream.Codec,
                SourceLanguage: null,
                Policy: "copy",
                Fidelity: "lossless",
                Reconstructable: true,
                OriginalParams: null,
                Container: layout.ContainerString,
                Files: ResolveVideoFiles(output: copyOutput, layout: layout, outputFiles: outputFiles),
                Sha256: null
            );

        // No copy rung: the first rung the ladder built for this stream is
        // the best-available transcoded payload — every other rung is a
        // lower-quality derivative of the same accepted loss.
        VideoOutputPlan? transcodeOutput = matched.FirstOrDefault();
        if (transcodeOutput is not null)
            return new(
                SourceStreamIndex: stream.Index,
                Kind: "video",
                SourceCodec: stream.Codec,
                SourceLanguage: null,
                Policy: "transcode",
                Fidelity: "lossy",
                Reconstructable: false,
                OriginalParams: BuildVideoOriginalParams(stream: stream),
                Container: layout.ContainerString,
                Files: ResolveVideoFiles(output: transcodeOutput, layout: layout, outputFiles: outputFiles),
                Sha256: null
            );

        return DroppedTrack(sourceStreamIndex: stream.Index, kind: "video", codec: stream.Codec, language: null);
    }

    private static JObject BuildVideoOriginalParams(VideoStreamInfo stream) =>
        new()
        {
            [propertyName: "codec"] = stream.Codec,
            [propertyName: "width"] = stream.Width,
            [propertyName: "height"] = stream.Height,
            [propertyName: "bit_depth"] = stream.BitDepth,
            [propertyName: "pixel_format"] = stream.PixelFormat,
            [propertyName: "bit_rate"] = stream.BitRateKbps > 0 ? stream.BitRateKbps * 1000 : null,
        };

    private static IReadOnlyList<string> ResolveVideoFiles(
        VideoOutputPlan output,
        BundleLayout layout,
        IReadOnlyList<string> outputFiles
    )
    {
        if (layout.IsSingleFile)
            return [layout.SingleFileName];

        Dictionary<string, string> tokens = TemplateResolver.VideoTokens(
            width: output.Width,
            height: output.Height,
            isHdrOutput: output.IsHdrOutput
        );
        string resolved = TemplateResolver.Resolve(template: output.SegmentNameTemplate, values: tokens);
        return FilesUnder(outputFiles: outputFiles, folder: FolderOf(resolvedTemplate: resolved));
    }

    // -----------------------------------------------------------------------
    // Audio — walk source.AudioStreams, never plan.AudioOutputs
    // -----------------------------------------------------------------------

    private static List<BlueprintTrack> BuildAudioTracks(
        IReadOnlyList<AudioStreamInfo> streams,
        AudioOutputPlan[] outputs,
        BundleLayout layout,
        IReadOnlyList<string> outputFiles
    )
    {
        List<BlueprintTrack> tracks = [];
        foreach (AudioStreamInfo stream in streams)
        {
            List<AudioOutputPlan> matched = outputs
                .Where(predicate: o => o.SourceStreamIndex == stream.Index)
                .ToList();
            tracks.Add(item: BuildAudioTrack(stream: stream, matched: matched, layout: layout, outputFiles: outputFiles));
        }
        return tracks;
    }

    private static BlueprintTrack BuildAudioTrack(
        AudioStreamInfo stream,
        List<AudioOutputPlan> matched,
        BundleLayout layout,
        IReadOnlyList<string> outputFiles
    )
    {
        AudioOutputPlan? copyOutput = matched.FirstOrDefault(predicate: o => o.Action == StreamAction.Copy);
        if (copyOutput is not null)
            return new(
                SourceStreamIndex: stream.Index,
                Kind: "audio",
                SourceCodec: stream.Codec,
                SourceLanguage: stream.Language,
                Policy: "copy",
                Fidelity: "lossless",
                Reconstructable: true,
                OriginalParams: null,
                Container: layout.ContainerString,
                Files: ResolveAudioFiles(output: copyOutput, layout: layout, outputFiles: outputFiles),
                Sha256: null
            );

        AudioOutputPlan? transcodeOutput = matched.FirstOrDefault();
        if (transcodeOutput is not null)
            return new(
                SourceStreamIndex: stream.Index,
                Kind: "audio",
                SourceCodec: stream.Codec,
                SourceLanguage: stream.Language,
                Policy: "transcode",
                Fidelity: "lossy",
                Reconstructable: false,
                OriginalParams: BuildAudioOriginalParams(stream: stream),
                Container: layout.ContainerString,
                Files: ResolveAudioFiles(output: transcodeOutput, layout: layout, outputFiles: outputFiles),
                Sha256: null
            );

        return DroppedTrack(sourceStreamIndex: stream.Index, kind: "audio", codec: stream.Codec, language: stream.Language);
    }

    private static JObject BuildAudioOriginalParams(AudioStreamInfo stream) =>
        new()
        {
            [propertyName: "codec"] = stream.Codec,
            [propertyName: "channels"] = stream.Channels,
            [propertyName: "sample_rate"] = stream.SampleRate,
            [propertyName: "bit_rate"] = stream.BitRateKbps > 0 ? stream.BitRateKbps * 1000 : null,
        };

    private static IReadOnlyList<string> ResolveAudioFiles(
        AudioOutputPlan output,
        BundleLayout layout,
        IReadOnlyList<string> outputFiles
    )
    {
        if (layout.IsSingleFile)
            return [layout.SingleFileName];

        Dictionary<string, string> tokens = TemplateResolver.AudioTokens(
            language: output.Language ?? "und",
            codecName: output.CodecToken,
            channels: output.Channels
        );
        string resolved = TemplateResolver.Resolve(template: output.SegmentNameTemplate, values: tokens);
        return FilesUnder(outputFiles: outputFiles, folder: FolderOf(resolvedTemplate: resolved));
    }

    // -----------------------------------------------------------------------
    // Subtitle — walk source.SubtitleStreams. Acquired subtitles (SourceIndex
    // -1 on the plan side) carry no backing source stream and are correctly
    // absent from this walk — the invariant only covers what the source
    // actually has.
    // -----------------------------------------------------------------------

    private static List<BlueprintTrack> BuildSubtitleTracks(
        IReadOnlyList<SubtitleStreamInfo> streams,
        SubtitleOutputPlan[] outputs,
        BundleLayout layout,
        IReadOnlyList<string> outputFiles
    )
    {
        List<BlueprintTrack> tracks = [];
        foreach (SubtitleStreamInfo stream in streams)
        {
            SubtitleOutputPlan? matched = outputs.FirstOrDefault(predicate: o =>
                o.SourceIndex == stream.Index
            );
            tracks.Add(item: BuildSubtitleTrack(stream: stream, matched: matched, layout: layout, outputFiles: outputFiles));
        }
        return tracks;
    }

    private static BlueprintTrack BuildSubtitleTrack(
        SubtitleStreamInfo stream,
        SubtitleOutputPlan? matched,
        BundleLayout layout,
        IReadOnlyList<string> outputFiles
    )
    {
        if (matched is null)
            return DroppedTrack(sourceStreamIndex: stream.Index, kind: "subtitle", codec: stream.Codec, language: stream.Language);

        if (matched.Policy == SubtitlePolicy.BurnIn)
            return new(
                SourceStreamIndex: stream.Index,
                Kind: "subtitle",
                SourceCodec: stream.Codec,
                SourceLanguage: stream.Language,
                Policy: "burn_in",
                Fidelity: "irreversible",
                Reconstructable: false,
                OriginalParams: null,
                Container: null,
                Files: [],
                Sha256: null
            );

        // A container-embedded copy (MKV single-file output) has no sidecar
        // of its own — the whole output file IS the artifact.
        SubtitleArtifact artifact =
            layout.IsSingleFile && matched.Policy == SubtitlePolicy.Copy
                ? new(Files: [layout.SingleFileName], Container: layout.ContainerString)
                : ResolveSubtitleArtifact(stream: stream, matched: matched, outputFiles: outputFiles);

        if (artifact.Files.Count == 0)
            return DroppedTrack(sourceStreamIndex: stream.Index, kind: "subtitle", codec: stream.Codec, language: stream.Language);

        return new(
            SourceStreamIndex: stream.Index,
            Kind: "subtitle",
            SourceCodec: stream.Codec,
            SourceLanguage: stream.Language,
            Policy: matched.Policy == SubtitlePolicy.Copy ? "copy" : "extract",
            Fidelity: "lossless",
            Reconstructable: true,
            OriginalParams: null,
            Container: artifact.Container,
            Files: artifact.Files,
            Sha256: null
        );
    }

    /// <summary>
    /// Resolves the single lossless-preservable artifact for a subtitle
    /// stream — see spec "Tracks and artifact selection": only ASS (text) or
    /// mks / idx+sub (bitmap) are ever a "preserved extract". A source that
    /// was extracted with a codec CONVERSION (e.g. an SRT/mov_text stream
    /// turned into WebVTT) has no admissible artifact here — a `.vtt` file
    /// can never be a track's <see cref="BlueprintTrack.Files"/> (spec rule
    /// 4, the OCR/player-sidecar exclusion) — so that stream falls through
    /// to "dropped" rather than pointing at a payload the spec forbids.
    /// </summary>
    private static SubtitleArtifact ResolveSubtitleArtifact(
        SubtitleStreamInfo stream,
        SubtitleOutputPlan matched,
        IReadOnlyList<string> outputFiles
    )
    {
        bool isBitmap = SubtitleClassifier.IsBitmapBased(codec: stream.Codec);
        bool isVobSub = stream.Codec.Equals(value: "dvd_subtitle", comparisonType: StringComparison.OrdinalIgnoreCase);
        bool isAss =
            stream.Codec.Equals(value: "ass", comparisonType: StringComparison.OrdinalIgnoreCase)
            || stream.Codec.Equals(value: "ssa", comparisonType: StringComparison.OrdinalIgnoreCase);

        string[] candidateExtensions =
            isBitmap ? (isVobSub ? ["idx", "sub"] : ["mks"])
            : isAss ? ["ass"]
            : ["srt"]; // Copy-policy extraction of an already-text SRT source.

        string language = stream.Language ?? matched.Language ?? "und";
        List<string> files = FindSubtitleSidecars(
            outputFiles: outputFiles,
            language: language,
            variant: matched.Variant,
            candidateExtensions: candidateExtensions
        );

        if (files.Count == 0)
            return SubtitleArtifact.None;

        string container =
            isBitmap ? (isVobSub ? "idx" : "mks")
            : isAss ? "ass"
            : "srt";
        return new(Files: files, Container: container);
    }

    private static List<string> FindSubtitleSidecars(
        IReadOnlyList<string> outputFiles,
        string language,
        string variant,
        string[] candidateExtensions
    )
    {
        string infix = $".{language}.{variant}.";
        HashSet<string> extensions = new(collection: candidateExtensions, comparer: StringComparer.OrdinalIgnoreCase);

        return outputFiles
            .Where(predicate: f =>
                f.StartsWith(value: "subtitles/", comparisonType: StringComparison.OrdinalIgnoreCase)
                && f.Contains(value: infix, comparisonType: StringComparison.OrdinalIgnoreCase)
                && extensions.Contains(item: Path.GetExtension(path: f).TrimStart(trimChar: '.'))
            )
            .OrderBy(keySelector: f => f, comparer: StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private readonly record struct SubtitleArtifact(IReadOnlyList<string> Files, string? Container)
    {
        public static readonly SubtitleArtifact None = new(Files: [], Container: null);
    }

    // -----------------------------------------------------------------------
    // Shared helpers
    // -----------------------------------------------------------------------

    private static BlueprintTrack DroppedTrack(
        int sourceStreamIndex,
        string kind,
        string codec,
        string? language
    ) =>
        new(
            SourceStreamIndex: sourceStreamIndex,
            Kind: kind,
            SourceCodec: codec,
            SourceLanguage: language,
            Policy: "dropped",
            Fidelity: "lost",
            Reconstructable: false,
            OriginalParams: null,
            Container: null,
            Files: [],
            Sha256: null
        );

    private static string FolderOf(string resolvedTemplate)
    {
        int slash = resolvedTemplate.LastIndexOf(value: '/');
        return slash < 0 ? resolvedTemplate : resolvedTemplate[..slash];
    }

    private static IReadOnlyList<string> FilesUnder(
        IReadOnlyList<string> outputFiles,
        string folder
    )
    {
        string prefix = folder + "/";
        return outputFiles
            .Where(predicate: f => f.StartsWith(value: prefix, comparisonType: StringComparison.OrdinalIgnoreCase))
            .OrderBy(keySelector: f => f, comparer: StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // -----------------------------------------------------------------------
    // Reconstruction command template — generated from the structured track
    // mapping (see spec), not frozen as a string.
    // -----------------------------------------------------------------------

    private static string BuildCommandTemplate(
        string targetContainer,
        IReadOnlyList<BlueprintTrack> tracks
    )
    {
        List<string> inputs = [];
        List<string> maps = [];
        int inputIndex = 0;

        foreach (BlueprintTrack track in tracks)
        {
            if (track.Files.Count == 0)
                continue; // dropped / burn_in — nothing survives to map back in.

            inputs.Add(item: $"-i \"{track.Files[index: 0]}\"");
            maps.Add(item: $"-map {inputIndex}:0");
            inputIndex++;
        }

        string extension = targetContainer == "matroska" ? "mkv" : targetContainer;
        return $"ffmpeg {string.Join(separator: ' ', values: inputs)} {string.Join(separator: ' ', values: maps)} -c copy \"reconstructed.{extension}\"".Trim();
    }

    private static List<string> BuildWarnings(IReadOnlyList<BlueprintTrack> tracks)
    {
        List<string> warnings = [];
        foreach (BlueprintTrack track in tracks)
        {
            if (track.Fidelity is "lossy" or "irreversible" or "lost")
                warnings.Add(
                    item: $"{track.Kind}[{track.SourceStreamIndex}]: {track.Policy} ({track.Fidelity}) — {DescribeLoss(track: track)}"
                );
        }
        return warnings;
    }

    private static string DescribeLoss(BlueprintTrack track) =>
        track.Policy switch
        {
            "transcode" =>
                $"re-encoded from {track.SourceCodec}; original quality is not recoverable.",
            "burn_in" => "burned into video pixels; original stream is not recoverable.",
            "dropped" => "produced no retained artifact; original stream is not recoverable.",
            _ => "not stream-copied; original quality is not recoverable.",
        };
}
