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

using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Naming;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using NoMercy.Storage;

namespace NoMercy.Encoder.Bundle;

public class ReconstructionWriter : IReconstructionWriter
{
    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        Formatting = Formatting.Indented,
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
    };

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public Reconstruction Build(
        MediaInfo mediaInfo,
        OutputPlan plan,
        BundleLayout layout,
        string? originalSourcePath = null
    )
    {
        ReconstructionSource source = BuildSource(mediaInfo, originalSourcePath);
        List<ReconstructionTrack> tracks = BuildTracks(mediaInfo, plan, layout);
        ReconstructionAssets assets = BuildAssets(mediaInfo, layout);
        string commandTemplate = BuildCommandTemplate(layout, tracks);
        List<string> warnings = BuildWarnings(tracks);

        return new(
            Version: 1,
            TargetContainer: layout.ContainerString,
            Source: source,
            Tracks: tracks,
            ExternalAssets: assets,
            CommandTemplate: commandTemplate,
            LossyWarnings: warnings
        );
    }

    public async Task WriteAsync(
        IStorage storage,
        string path,
        MediaInfo mediaInfo,
        OutputPlan plan,
        BundleLayout layout,
        CancellationToken ct,
        string? originalSourcePath = null
    )
    {
        Reconstruction reconstruction = Build(mediaInfo, plan, layout, originalSourcePath);
        string json = JsonConvert.SerializeObject(reconstruction, SerializerSettings);
        await storage.WriteAsync(path, Encoding.UTF8.GetBytes(json), ct);
    }

    public async Task<Reconstruction?> ReadAsync(
        IStorage storage,
        string path,
        CancellationToken ct
    )
    {
        if (!storage.Exists(path))
            return null;

        byte[] bytes = await storage.ReadAsync(path, ct);
        string json = Encoding.UTF8.GetString(bytes);
        return JsonConvert.DeserializeObject<Reconstruction>(json, SerializerSettings);
    }

    // -----------------------------------------------------------------------
    // Source fingerprint
    // -----------------------------------------------------------------------

    private static ReconstructionSource BuildSource(MediaInfo mediaInfo, string? originalSourcePath)
    {
        // MediaInfo.FilePath is whatever the analyzer was pointed at, which for a
        // remote source is a local staging lease that is released as soon as the
        // encode ends. Naming it here described the encode by a file that no
        // longer exists — the one thing reconstruction data must never do.
        string sourcePath = originalSourcePath ?? mediaInfo.FilePath;
        string filename = Path.GetFileName(sourcePath);

        return new(
            OriginalPath: sourcePath,
            OriginalFilename: filename,
            SizeBytes: mediaInfo.FileSizeBytes,
            // sha256: not computed — would require a full re-read of the source
            // file. Deferred until a streaming hasher is wired into the analyzer
            // pipeline so the hash comes for free on the same byte stream.
            Sha256: null,
            DurationSeconds: mediaInfo.Duration.TotalSeconds,
            Container: mediaInfo.Format,
            // Raw ffprobe JObject is not preserved on MediaInfo — the analyzer
            // parses into typed structs and discards the raw JSON. Deferred.
            Ffprobe: null
        );
    }

    // -----------------------------------------------------------------------
    // Track building
    // -----------------------------------------------------------------------

    private static List<ReconstructionTrack> BuildTracks(
        MediaInfo mediaInfo,
        OutputPlan plan,
        BundleLayout layout
    )
    {
        List<ReconstructionTrack> tracks = [];

        BuildVideoTracks(mediaInfo, plan, layout, tracks);
        BuildAudioTracks(mediaInfo, plan, layout, tracks);
        BuildSubtitleTracks(mediaInfo, plan, layout, tracks);

        return tracks;
    }

    private static void BuildVideoTracks(
        MediaInfo mediaInfo,
        OutputPlan plan,
        BundleLayout layout,
        List<ReconstructionTrack> tracks
    )
    {
        // Each VideoOutputPlan maps to a source video stream by position
        for (int i = 0; i < plan.VideoOutputs.Length; i++)
        {
            VideoOutputPlan output = plan.VideoOutputs[i];

            // Resolve source stream: use position index, fall back to index 0
            VideoStreamInfo? source =
                i < mediaInfo.VideoStreams.Count ? mediaInfo.VideoStreams[i] : null;

            string sourceCodec = source?.Codec ?? "unknown";
            bool isCopy = string.Equals(
                output.EncoderName,
                "copy",
                StringComparison.OrdinalIgnoreCase
            );

            string fidelity = isCopy ? "lossless" : "lossy";
            string? lossyReason = isCopy
                ? null
                : $"video re-encoded: {sourceCodec} → {output.EncoderName}";

            tracks.Add(
                new(
                    Kind: "video",
                    SourceStreamIndex: source?.Index ?? i,
                    SourceCodec: sourceCodec,
                    Policy: isCopy ? "copy" : "transcode",
                    OutputCodec: output.EncoderName,
                    BundleFiles: ResolveVideoBundleFiles(layout, output),
                    ConcatMethod: layout.IsSingleFile ? "none" : "hls_segment_concat",
                    Metadata: null,
                    Fidelity: fidelity,
                    LossyReason: lossyReason
                )
            );
        }
    }

    private static void BuildAudioTracks(
        MediaInfo mediaInfo,
        OutputPlan plan,
        BundleLayout layout,
        List<ReconstructionTrack> tracks
    )
    {
        foreach (AudioOutputPlan output in plan.AudioOutputs)
        {
            // Match source stream by language when possible
            AudioStreamInfo? source = MatchAudioSource(mediaInfo, output);
            string sourceCodec = source?.Codec ?? "unknown";

            string fidelity;
            string policy;
            string? lossyReason;

            switch (output.Action)
            {
                case StreamAction.Copy:
                    fidelity = "lossless";
                    policy = "copy";
                    lossyReason = null;
                    break;

                case StreamAction.Transcode:
                    fidelity = "lossy";
                    policy = "transcode";
                    lossyReason = $"audio re-encoded: {sourceCodec} → {output.EncoderName}";
                    break;

                case StreamAction.Extract:
                    // Extract preserves codec — lossless
                    fidelity = "lossless";
                    policy = "extract";
                    lossyReason = null;
                    break;

                default:
                    fidelity = "lossy";
                    policy = "transcode";
                    lossyReason = $"audio processed: {sourceCodec} → {output.EncoderName}";
                    break;
            }

            tracks.Add(
                new(
                    Kind: "audio",
                    SourceStreamIndex: source?.Index ?? -1,
                    SourceCodec: sourceCodec,
                    Policy: policy,
                    OutputCodec: output.EncoderName,
                    BundleFiles: ResolveAudioBundleFiles(layout, output),
                    ConcatMethod: layout.IsSingleFile ? "none" : "hls_segment_concat",
                    Metadata: null,
                    Fidelity: fidelity,
                    LossyReason: lossyReason
                )
            );
        }
    }

    private static void BuildSubtitleTracks(
        MediaInfo mediaInfo,
        OutputPlan plan,
        BundleLayout layout,
        List<ReconstructionTrack> tracks
    )
    {
        foreach (SubtitleOutputPlan output in plan.SubtitleOutputs)
        {
            // Acquired subtitles: SourceIndex == -1 means no source stream in file
            bool isAcquired = output.SourceIndex < 0;

            if (isAcquired)
            {
                tracks.Add(
                    new(
                        Kind: "subtitle",
                        SourceStreamIndex: output.SourceIndex,
                        SourceCodec: "n/a",
                        Policy: "acquire",
                        OutputCodec: SubtitleCodecTypeToString(output.OutputCodec),
                        BundleFiles: [],
                        ConcatMethod: "none",
                        Metadata: null,
                        Fidelity: "external",
                        LossyReason: null
                    )
                );
                continue;
            }

            SubtitleStreamInfo? source = mediaInfo.SubtitleStreams.FirstOrDefault(s =>
                s.Index == output.SourceIndex
            );
            string sourceCodec = source?.Codec ?? "unknown";

            string fidelity;
            string policy;
            string? lossyReason;

            if (output.Policy == SubtitlePolicy.BurnIn)
            {
                // Burned into video — the separate subtitle stream is gone
                fidelity = "irreversible";
                policy = "burn_in";
                lossyReason = "subtitle burned into video pixels; original stream not recoverable";
            }
            else if (
                output.Action == StreamAction.Copy
                || output.OutputCodec == SubtitleCodecType.Copy
            )
            {
                fidelity = "lossless";
                policy = "copy";
                lossyReason = null;
            }
            else
            {
                // Extract or transcode — check if codec changed
                string outputCodecStr = SubtitleCodecTypeToString(output.OutputCodec);
                bool sameCodec = string.Equals(
                    NormalizeSubtitleCodec(sourceCodec),
                    NormalizeSubtitleCodec(outputCodecStr),
                    StringComparison.OrdinalIgnoreCase
                );

                if (sameCodec)
                {
                    fidelity = "lossless";
                    policy = "extract";
                    lossyReason = null;
                }
                else
                {
                    fidelity = "lossy";
                    policy = "extract";
                    lossyReason =
                        $"subtitle converted: {sourceCodec} → {outputCodecStr} (typesetting or image data may be lost)";
                }
            }

            tracks.Add(
                new(
                    Kind: "subtitle",
                    SourceStreamIndex: output.SourceIndex,
                    SourceCodec: sourceCodec,
                    Policy: policy,
                    OutputCodec: SubtitleCodecTypeToString(output.OutputCodec),
                    BundleFiles: [],
                    ConcatMethod: "none",
                    Metadata: null,
                    Fidelity: fidelity,
                    LossyReason: lossyReason
                )
            );
        }
    }

    // -----------------------------------------------------------------------
    // External assets
    // -----------------------------------------------------------------------

    private static ReconstructionAssets BuildAssets(MediaInfo mediaInfo, BundleLayout layout)
    {
        List<string> fonts = [];
        List<string> attachments = [];

        foreach (AttachmentInfo att in mediaInfo.Attachments)
        {
            if (att.Filename is null)
                continue;

            bool isFont =
                att.MimeType is not null
                && (
                    att.MimeType.Contains("font", StringComparison.OrdinalIgnoreCase)
                    || att.MimeType.Contains("truetype", StringComparison.OrdinalIgnoreCase)
                    || att.MimeType.Contains("opentype", StringComparison.OrdinalIgnoreCase)
                );

            if (isFont)
                fonts.Add(att.Filename);
            else
                attachments.Add(att.Filename);
        }

        List<ReconstructionChapter> chapters = [];
        foreach (ChapterInfo ch in mediaInfo.Chapters)
        {
            chapters.Add(
                new(Start: ch.Start.TotalSeconds, End: ch.End.TotalSeconds, Title: ch.Title)
            );
        }

        return new(Fonts: fonts, CoverArt: [], Chapters: chapters, Attachments: attachments);
    }

    // -----------------------------------------------------------------------
    // Command template
    // -----------------------------------------------------------------------

    private static string BuildCommandTemplate(
        BundleLayout layout,
        List<ReconstructionTrack> tracks
    )
    {
        // Build a representative ffmpeg command template that a future CLI tool
        // can fill in to produce a single-file equivalent of the source.
        if (layout.IsSingleFile)
            return $"ffmpeg -i {{input}} [track_options] \"{layout.SingleFileName}\"";

        string masterPath = $"{layout.BundleDirectory}/{layout.MasterPlaylistName}";
        return $"ffmpeg [hls_concat_inputs:{masterPath}] [track_options] {{output}}";
    }

    // -----------------------------------------------------------------------
    // Lossy warnings
    // -----------------------------------------------------------------------

    private static List<string> BuildWarnings(List<ReconstructionTrack> tracks)
    {
        List<string> warnings = [];
        foreach (ReconstructionTrack track in tracks)
        {
            if (track.Fidelity is "lossy" or "irreversible" && track.LossyReason is not null)
                warnings.Add($"{track.Kind}[{track.SourceStreamIndex}]: {track.LossyReason}");
        }
        return warnings;
    }

    // -----------------------------------------------------------------------
    // Bundle file path helpers (best-effort from plan data)
    // -----------------------------------------------------------------------

    private static IReadOnlyList<string> ResolveVideoBundleFiles(
        BundleLayout layout,
        VideoOutputPlan output
    )
    {
        if (layout.IsSingleFile)
            return [layout.SingleFileName];

        // Derive a representative path from the plan's MapLabel / layout.
        // Full segment enumeration is not available at finalize time from the
        // plan alone; the manifest.json file list is the authoritative index.
        string dir = $"{layout.BundleDirectory}/video";
        return [$"{dir}/{layout.MediaKey}_video_*"];
    }

    private static IReadOnlyList<string> ResolveAudioBundleFiles(
        BundleLayout layout,
        AudioOutputPlan output
    )
    {
        if (layout.IsSingleFile)
            return [layout.SingleFileName];

        string lang = output.Language ?? "und";
        string dir = $"{layout.BundleDirectory}/audio/{lang}";
        return [$"{dir}/{layout.MediaKey}_{lang}_*"];
    }

    // -----------------------------------------------------------------------
    // Codec helpers
    // -----------------------------------------------------------------------

    private static AudioStreamInfo? MatchAudioSource(MediaInfo mediaInfo, AudioOutputPlan output)
    {
        if (output.Language is not null)
        {
            AudioStreamInfo? byLang = mediaInfo.AudioStreams.FirstOrDefault(a =>
                string.Equals(a.Language, output.Language, StringComparison.OrdinalIgnoreCase)
            );
            if (byLang is not null)
                return byLang;
        }
        return mediaInfo.AudioStreams.FirstOrDefault();
    }

    private static string SubtitleCodecTypeToString(SubtitleCodecType codec) =>
        codec switch
        {
            SubtitleCodecType.WebVtt => "webvtt",
            SubtitleCodecType.Srt => "subrip",
            SubtitleCodecType.Ass => "ass",
            SubtitleCodecType.Pgs => "hdmv_pgs_subtitle",
            SubtitleCodecType.Copy => "copy",
            _ => codec.ToString().ToLowerInvariant(),
        };

    private static string NormalizeSubtitleCodec(string codec) =>
        codec.ToLowerInvariant() switch
        {
            "vtt" or "webvtt" => "webvtt",
            "srt" or "subrip" => "subrip",
            "ssa" or "ass" => "ass",
            "pgs" or "hdmv_pgs_subtitle" => "pgs",
            string other => other,
        };
}
