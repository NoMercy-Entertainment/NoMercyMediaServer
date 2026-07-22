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

using System.Globalization;
using System.Text;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Subtitles;
using NoMercy.NmSystem.Extensions;
using NoMercy.Storage;

namespace NoMercy.Encoder.Output;

public class PlaylistGenerator : IPlaylistGenerator
{
    /// <summary>
    /// Returns the minimum HLS version number required for the given combination
    /// of active features. The caller takes the max of all that apply.
    ///
    /// Rules (per the HLS specification):
    ///   v3 — baseline MPEG-TS (no subtitles group, no fMP4, no chapter date-ranges).
    ///   v6 — at least one EXT-X-MEDIA:TYPE=SUBTITLES group in the master playlist.
    ///   v7 — any variant uses fMP4 segments (hls_segment_type fmp4).
    ///   v8 — EXT-X-DATERANGE entries driven by chapter data (Phase 4.5).
    /// </summary>
    internal static int ComputeMasterVersion(
        bool hasSubsGroup,
        bool hasFmp4,
        bool hasChapterDateRanges
    )
    {
        int version = 3;
        if (hasSubsGroup)
            version = Math.Max(val1: version, val2: 6);
        if (hasFmp4)
            version = Math.Max(val1: version, val2: 7);
        if (hasChapterDateRanges)
            version = Math.Max(val1: version, val2: 8);
        return version;
    }

    /// <summary>
    /// The metrics-dict key for a video variant: its resolved playlist path,
    /// unique per resolution/HDR. MUST be used by whoever POPULATES the metrics
    /// dict (HlsOutputStrategy) and whoever LOOKS UP (this generator), so a rung
    /// that re-plans as MapLabel "[v0]" in its own bundle never collides with
    /// another variant and collapses the whole ladder onto one BANDWIDTH.
    /// </summary>
    public static string VideoVariantKey(VideoOutputPlan video) =>
        TemplateResolver.Resolve(
            template: video.PlaylistNameTemplate,
            values: TemplateResolver.VideoTokens(width: video.Width, height: video.Height, isHdrOutput: video.IsHdrOutput)
        );

    /// <summary>The audio-variant metrics key — same rationale as <see cref="VideoVariantKey"/>.</summary>
    public static string AudioVariantKey(AudioOutputPlan audio) =>
        TemplateResolver.Resolve(
            template: audio.PlaylistNameTemplate,
            values: TemplateResolver.AudioTokens(language: audio.Language ?? "und", codecName: audio.CodecToken, channels: audio.Channels)
        );

    public string GenerateMasterPlaylist(
        OutputPlan plan,
        string mediaTitle,
        Dictionary<string, VariantMetrics> videoMetrics,
        Dictionary<string, VariantMetrics> audioMetrics
    )
    {
        HlsPlanOptions hlsOptions = plan.HlsOptions ?? new HlsPlanOptions();

        bool hasFmp4 = hlsOptions.SegmentType.Equals(value: "fmp4", comparisonType: StringComparison.OrdinalIgnoreCase);
        // WebVTT outputs only contribute a subs group when the slicer is
        // going to emit the m3u8 wrapper they need. With chunking off the
        // .vtt extracts still exist but there is no playable HLS playlist
        // entry to advertise — referencing the raw .vtt would 404 on
        // spec-compliant clients.
        bool hasSubsGroup = plan.SubtitleOutputs.Any(predicate: s =>
            s.Action is StreamAction.Extract or StreamAction.Copy
            && (s.OutputCodec is not SubtitleCodecType.WebVtt || plan.EmitSubtitleWebVttChunks)
        );

        bool hasChapterDateRanges = plan.Chapters is { Count: > 0 };

        int version = ComputeMasterVersion(hasSubsGroup: hasSubsGroup, hasFmp4: hasFmp4, hasChapterDateRanges: hasChapterDateRanges);

        StringBuilder sb = new();
        sb.AppendLine(value: "#EXTM3U");
        sb.AppendLine(handler: $"#EXT-X-VERSION:{version}");

        // Emit #EXT-X-INDEPENDENT-SEGMENTS when the option is on OR when fMP4
        // segments are used (fMP4 requires independent segments per the spec).
        if (hlsOptions.IndependentSegments || hasFmp4)
            sb.AppendLine(value: "#EXT-X-INDEPENDENT-SEGMENTS");

        sb.AppendLine();

        // Audio groups — keyed by codec for GROUP-ID
        string audioGroupId = "audio_aac";
        if (plan.AudioOutputs.Length > 0)
        {
            audioGroupId = $"audio_{plan.AudioOutputs[0].CodecToken}";
        }

        bool defaultAudioEmitted = false;
        // Tracks whether any EXT-X-MEDIA:TYPE=AUDIO line was actually written —
        // NOT just whether plan.AudioOutputs is non-empty. A rendition can be
        // planned but never materialise (missing segments, zero bandwidth) and
        // gets skipped below; the STREAM-INF AUDIO="..." attribute must follow
        // that same fate so it never references a group with zero members.
        bool audioGroupEmitted = false;
        foreach (AudioOutputPlan audio in plan.AudioOutputs)
        {
            if (audio.Action is not (StreamAction.Copy or StreamAction.Transcode))
                continue;

            // Resolve this rendition's playlist path first — it is the metrics
            // key (keyed by path, NOT MapLabel, so multiple audio renditions
            // never collide on a shared label).
            Dictionary<string, string> tokens = TemplateResolver.AudioTokens(
                language: audio.Language ?? "und",
                codecName: audio.CodecToken,
                channels: audio.Channels
            );

            string playlistResolved = TemplateResolver.Resolve(template: audio.PlaylistNameTemplate, values: tokens);

            // Skip audio variants whose segments never materialised. The
            // analyzer returns zero bandwidth when the playlist or its
            // segments are missing on disk — listing those in the master
            // makes hls.js / VLC bail on the first variant fetch.
            VariantMetrics audMetrics = audioMetrics.GetValueOrDefault(key: playlistResolved, defaultValue: new(PeakBandwidth: 0, AverageBandwidth: 0));
            if (audMetrics.PeakBandwidth == 0)
                continue;

            string subDir = StoragePathHelpers.GetParent(path: playlistResolved) ?? playlistResolved;
            string playlistFile = StoragePathHelpers.GetName(path: playlistResolved);

            string uri = $"{subDir}/{playlistFile}.m3u8";
            string language = audio.Language ?? "und";
            string displayName = GetAudioDisplayName(language: language);
            bool isDefault = !defaultAudioEmitted;
            defaultAudioEmitted = true;
            audioGroupEmitted = true;

            sb.AppendLine(
                handler: $"#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=\"{audioGroupId}\",LANGUAGE=\"{language}\",AUTOSELECT=YES,DEFAULT={YesNo(value: isDefault)},URI=\"{uri}\",NAME=\"{displayName}\""
            );
        }

        // Subtitle groups — one EXT-X-MEDIA per plan. URI now reflects
        // language + variant + codec, so the "English (Full)" / "(Sign)" /
        // "(SDH)" tracks of a single source land as three distinct entries
        // pointing at three distinct files instead of collapsing to one.
        // WebVTT entries are filtered out when chunking is disabled — same
        // reasoning as hasSubsGroup above.
        SubtitleOutputPlan[] activeSubs = plan
            .SubtitleOutputs.Where(predicate: s =>
                s.Action is StreamAction.Extract or StreamAction.Copy
                && (s.OutputCodec is not SubtitleCodecType.WebVtt || plan.EmitSubtitleWebVttChunks)
            )
            .ToArray();

        if (activeSubs.Length > 0)
        {
            foreach (SubtitleOutputPlan sub in activeSubs)
            {
                string lang = sub.Language ?? "und";
                string displayName = GetSubtitleDisplayName(language: lang, variant: sub.Variant);
                string subsUri = GetSubtitlePlaylistUri(sub: sub);
                bool isForced = string.Equals(
                    a: sub.Variant,
                    b: "sign",
                    comparisonType: StringComparison.OrdinalIgnoreCase
                );

                sb.AppendLine(
                    handler: $"#EXT-X-MEDIA:TYPE=SUBTITLES,GROUP-ID=\"subs\",NAME=\"{displayName}\",LANGUAGE=\"{lang}\",DEFAULT=NO,AUTOSELECT=YES,FORCED={YesNo(value: isForced)},URI=\"{subsUri}\""
                );
            }

            sb.AppendLine();
        }

        // Video variants with measured bandwidth
        foreach (VideoOutputPlan video in plan.VideoOutputs)
        {
            string? videoCodecTag = GetVideoCodecTag(video: video);
            string? audioCodecTag =
                plan.AudioOutputs.Length > 0 ? GetAudioCodecTag(audio: plan.AudioOutputs[0]) : null;
            string codecsAttr = BuildCodecsAttribute(videoCodec: videoCodecTag, audioCodec: audioCodecTag);

            // Resolve this variant's own playlist path up front — it is the key
            // into videoMetrics (HlsOutputStrategy stored each variant's measured
            // bitrate under the SAME resolved path). Keying by MapLabel instead
            // collapses every variant onto "[v0]" and gives them all one shared
            // BANDWIDTH.
            Dictionary<string, string> tokens = TemplateResolver.VideoTokens(
                width: video.Width,
                height: video.Height,
                isHdrOutput: video.IsHdrOutput
            );
            string playlistResolved = TemplateResolver.Resolve(template: video.PlaylistNameTemplate, values: tokens);
            string subDir = StoragePathHelpers.GetParent(path: playlistResolved) ?? playlistResolved;
            string playlistFile = StoragePathHelpers.GetName(path: playlistResolved);

            // Use measured bandwidth. Apple requires BANDWIDTH = peak, AVERAGE-BANDWIDTH = average.
            // Combine video + audio bandwidth for the STREAM-INF (Apple spec section 4.10).
            VariantMetrics vidMetrics = videoMetrics.GetValueOrDefault(key: playlistResolved, defaultValue: new(PeakBandwidth: 0, AverageBandwidth: 0));

            // Skip video variants whose segments never materialised — bundle
            // got cancelled / failed / didn't publish. Listing them in the
            // master makes VLC and hls.js bail on first init.mp4 fetch with
            // a 404. The analyzer returns zero bandwidth for a missing
            // playlist or empty segment dir.
            if (vidMetrics.PeakBandwidth == 0)
                continue;

            VariantMetrics audMetrics = new(PeakBandwidth: 0, AverageBandwidth: 0);
            if (plan.AudioOutputs.Length > 0)
            {
                AudioOutputPlan primaryAudio = plan.AudioOutputs[0];
                Dictionary<string, string> audioTokens = TemplateResolver.AudioTokens(
                    language: primaryAudio.Language ?? "und",
                    codecName: primaryAudio.CodecToken,
                    channels: primaryAudio.Channels
                );
                string audioResolved = TemplateResolver.Resolve(
                    template: primaryAudio.PlaylistNameTemplate,
                    values: audioTokens
                );
                audMetrics = audioMetrics.GetValueOrDefault(key: audioResolved, defaultValue: new(PeakBandwidth: 0, AverageBandwidth: 0));
            }

            int peakBandwidth = vidMetrics.PeakBandwidth + audMetrics.PeakBandwidth;
            int avgBandwidth = vidMetrics.AverageBandwidth + audMetrics.AverageBandwidth;

            // VIDEO-RANGE labels the colour pipeline (PQ/HLG = HDR transfer,
            // SDR otherwise) — not the bit depth. 10-bit anime / 10-bit BT.709
            // remux is SDR; mislabelling them as PQ makes hls.js reject the
            // master with manifestIncompatibleCodecsError because the segments'
            // actual transfer characteristics don't match.
            string videoRange = video.IsHdrOutput ? "PQ" : "SDR";
            string frameRate = video.FrameRate.ToString(format: "F3", provider: CultureInfo.InvariantCulture);

            string subsAttr =
                activeSubs.Length > 0 ? ",SUBTITLES=\"subs\"" : ",CLOSED-CAPTIONS=NONE";

            // Never point AUDIO="..." at a group with zero EXT-X-MEDIA members —
            // a title with genuinely no audio must emit neither the group nor
            // this reference, not a dangling one a player fails to resolve.
            string audioAttr = audioGroupEmitted ? $",AUDIO=\"{audioGroupId}\"" : string.Empty;

            sb.AppendLine(
                handler: $"#EXT-X-STREAM-INF:BANDWIDTH={peakBandwidth},AVERAGE-BANDWIDTH={avgBandwidth},RESOLUTION={video.Width}x{video.Height},FRAME-RATE={frameRate}{codecsAttr},VIDEO-RANGE={videoRange}{audioAttr}{subsAttr}"
            );
            sb.AppendLine(handler: $"{subDir}/{playlistFile}.m3u8");
        }

        // Emit EXT-X-DATERANGE chapter markers (HLS v8).
        // START-DATE is a UTC ISO 8601 instant derived from epoch + chapter offset,
        // so players can render a chapter timeline without decoding position.
        if (plan.Chapters is { Count: > 0 } chapters)
        {
            DateTimeOffset epoch = DateTimeOffset.UnixEpoch;

            for (int i = 0; i < chapters.Count; i++)
            {
                ChapterInfo chapter = chapters[index: i];
                double startSeconds = chapter.Start.TotalSeconds;
                double endSeconds =
                    i + 1 < chapters.Count
                        ? chapters[index: i + 1].Start.TotalSeconds
                        : chapter.End.TotalSeconds;
                double durationSeconds = endSeconds - startSeconds;

                DateTimeOffset startDate = epoch.AddSeconds(seconds: startSeconds);
                string startDateIso = startDate.ToString(
                    format: "yyyy-MM-ddTHH:mm:ss.fffZ",
                    formatProvider: CultureInfo.InvariantCulture
                );
                string title = chapter.Title ?? $"Chapter {i + 1}";
                string escapedTitle = title.Replace(oldValue: "\"", newValue: "\\\"");
                string durationFormatted = durationSeconds.ToString(
                    format: "F3",
                    provider: CultureInfo.InvariantCulture
                );

                sb.AppendLine(
                    handler: $"#EXT-X-DATERANGE:ID=\"ch{i}\",START-DATE=\"{startDateIso}\",DURATION={durationFormatted},X-COM-NOMERCY-CHAPTER-TITLE=\"{escapedTitle}\""
                );
            }
        }

        return sb.ToString();
    }

    private static string GetAudioDisplayName(string language) =>
        Culture.EnglishLanguageName(code: language);

    private static string? GetVideoCodecTag(VideoOutputPlan video) =>
        HlsCodecsStringBuilder.VideoCodecString(
            encoderName: video.EncoderName,
            profile: video.Profile,
            level: video.Level,
            tenBit: video.TenBit,
            width: video.Width,
            height: video.Height,
            frameRate: video.FrameRate
        );

    private static string? GetAudioCodecTag(AudioOutputPlan audio) =>
        HlsCodecsStringBuilder.AudioCodecString(encoderName: audio.EncoderName);

    /// <summary>
    /// Builds the ",CODECS=&quot;...&quot;" clause (leading comma included) from
    /// whichever of the video/audio tags are known. Returns an empty string —
    /// omitting the whole attribute — when neither is known (e.g. both streams
    /// are copy-mode), which is spec-legal and safer than a partially-guessed value.
    /// </summary>
    private static string BuildCodecsAttribute(string? videoCodec, string? audioCodec)
    {
        List<string> parts = [];
        if (videoCodec is not null)
            parts.Add(item: videoCodec);
        if (audioCodec is not null)
            parts.Add(item: audioCodec);

        return parts.Count > 0 ? $",CODECS=\"{string.Join(separator: ",", values: parts)}\"" : string.Empty;
    }

    private static string YesNo(bool value) => value ? "YES" : "NO";

    // ------------------------------------------------------------------
    // Subtitle helpers
    // ------------------------------------------------------------------

    private static string GetSubtitleDisplayName(string language, string variant = "full")
    {
        string langName = Culture.EnglishLanguageName(code: language);

        return variant.ToLowerInvariant() switch
        {
            "sign" => $"{langName} (Signs & Songs)",
            "sdh" => $"{langName} (SDH)",
            "alt" => $"{langName} (Alt)",
            _ => langName,
        };
    }

    // ASS/SRT sidecars play directly from the file URI; only WebVTT needs
    // a single-segment .m3u8 wrapper because that's what HLS spec requires
    // for EXT-X-MEDIA. All subtitle assets live under subtitles/{lang}/ so
    // a season with many tracks doesn't flatten into one giant directory.
    internal static string GetSubtitlePlaylistUri(SubtitleOutputPlan sub)
    {
        string lang = sub.Language ?? "und";
        string variant = string.IsNullOrEmpty(value: sub.Variant) ? "full" : sub.Variant;

        return sub.OutputCodec switch
        {
            SubtitleCodecType.Ass => $"subtitles/{lang}/{variant}.ass",
            SubtitleCodecType.Srt => $"subtitles/{lang}/{variant}.srt",
            _ => $"subtitles/{lang}/{variant}.m3u8",
        };
    }

    /// <summary>
    /// Emits the per-subtitle media playlist (.m3u8) referencing the WebVTT
    /// segments produced by <see cref="WebVttSegmenter"/>. Playlist lives at
    /// <c>subtitles/{lang}/{variant}.m3u8</c>; segment URIs are relative to
    /// that folder (<c>{variant}_NNNNN.vtt</c>), so the m3u8 is portable as
    /// long as it stays alongside its segments.
    /// </summary>
    public static string GenerateSubtitleMediaPlaylist(
        SubtitleOutputPlan sub,
        IReadOnlyList<WebVttSegment> segments,
        int segmentDurationSeconds,
        string segmentUriPrefix = ""
    )
    {
        string variant = string.IsNullOrEmpty(value: sub.Variant) ? "full" : sub.Variant;

        StringBuilder sb = new();
        sb.AppendLine(value: "#EXTM3U");
        sb.AppendLine(value: "#EXT-X-VERSION:3");
        sb.AppendLine(handler: $"#EXT-X-TARGETDURATION:{segmentDurationSeconds}");
        sb.AppendLine(value: "#EXT-X-PLAYLIST-TYPE:VOD");

        foreach (WebVttSegment seg in segments)
        {
            double actualDuration = (seg.EndTime - seg.StartTime).TotalSeconds;
            string segFile = $"{variant}_{seg.Index:D5}.vtt";
            string uri = string.IsNullOrEmpty(value: segmentUriPrefix)
                ? segFile
                : $"{segmentUriPrefix}/{segFile}";

            sb.AppendLine(
                handler: $"#EXTINF:{actualDuration.ToString(format: "F3", provider: CultureInfo.InvariantCulture)},"
            );
            sb.AppendLine(value: uri);
        }

        sb.AppendLine(value: "#EXT-X-ENDLIST");
        return sb.ToString();
    }

    /// <summary>
    /// Emits a minimal media playlist for a sidecar .ass file (single entry,
    /// no segmentation). The NoMercy player loads the .ass directly via
    /// libass-wasm; the m3u8 is the HLS hook that advertises it.
    /// </summary>
    public static string GenerateAssMediaPlaylist(
        SubtitleOutputPlan sub,
        string assFileName,
        int segmentDurationSeconds
    )
    {
        string lang = sub.Language ?? "und";
        StringBuilder sb = new();
        sb.AppendLine(value: "#EXTM3U");
        sb.AppendLine(value: "#EXT-X-VERSION:3");
        sb.AppendLine(handler: $"#EXT-X-TARGETDURATION:{segmentDurationSeconds}");
        sb.AppendLine(value: "#EXT-X-PLAYLIST-TYPE:VOD");
        sb.AppendLine(handler: $"#EXTINF:{segmentDurationSeconds}.000,");
        sb.AppendLine(value: assFileName);
        sb.AppendLine(value: "#EXT-X-ENDLIST");
        return sb.ToString();
    }
}
