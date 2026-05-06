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
            version = Math.Max(version, 6);
        if (hasFmp4)
            version = Math.Max(version, 7);
        if (hasChapterDateRanges)
            version = Math.Max(version, 8);
        return version;
    }

    public string GenerateMasterPlaylist(
        OutputPlan plan,
        string mediaTitle,
        Dictionary<string, HlsVariantAnalyzer.VariantMetrics> videoMetrics,
        Dictionary<string, HlsVariantAnalyzer.VariantMetrics> audioMetrics
    )
    {
        HlsPlanOptions hlsOptions = plan.HlsOptions ?? new HlsPlanOptions();

        bool hasFmp4 = hlsOptions.SegmentType.Equals("fmp4", StringComparison.OrdinalIgnoreCase);
        bool hasSubsGroup = plan.SubtitleOutputs.Any(s =>
            s.Action is StreamAction.Extract or StreamAction.Copy
        );

        bool hasChapterDateRanges = plan.Chapters is { Count: > 0 };

        int version = ComputeMasterVersion(hasSubsGroup, hasFmp4, hasChapterDateRanges);

        StringBuilder sb = new();
        sb.AppendLine("#EXTM3U");
        sb.AppendLine($"#EXT-X-VERSION:{version}");

        // Emit #EXT-X-INDEPENDENT-SEGMENTS when the option is on OR when fMP4
        // segments are used (fMP4 requires independent segments per the spec).
        if (hlsOptions.IndependentSegments || hasFmp4)
            sb.AppendLine("#EXT-X-INDEPENDENT-SEGMENTS");

        sb.AppendLine();

        // Audio groups — keyed by codec for GROUP-ID
        string audioGroupId = "audio_aac";
        if (plan.AudioOutputs.Length > 0)
        {
            string firstCodecName = plan.AudioOutputs[0]
                .EncoderName.Replace("libfdk_", "")
                .Replace("lib", "");
            audioGroupId = $"audio_{firstCodecName}";
        }

        foreach (AudioOutputPlan audio in plan.AudioOutputs)
        {
            if (audio.Action is not (StreamAction.Copy or StreamAction.Transcode))
                continue;

            string codecName = audio.EncoderName.Replace("libfdk_", "").Replace("lib", "");
            Dictionary<string, string> tokens = TemplateResolver.AudioTokens(
                audio.Language ?? "und",
                codecName,
                audio.Channels
            );

            string playlistResolved = TemplateResolver.Resolve(audio.PlaylistNameTemplate, tokens);
            string subDir = StoragePathHelpers.GetParent(playlistResolved) ?? playlistResolved;
            string playlistFile = StoragePathHelpers.GetName(playlistResolved);

            string uri = $"{subDir}/{playlistFile}.m3u8";
            string language = audio.Language ?? "und";
            string displayName = GetAudioDisplayName(language);
            bool isDefault = audio == plan.AudioOutputs[0];

            sb.AppendLine(
                $"#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=\"{audioGroupId}\",LANGUAGE=\"{language}\",AUTOSELECT=YES,DEFAULT={YesNo(isDefault)},URI=\"{uri}\",NAME=\"{displayName}\""
            );
        }

        // Subtitle groups — one EXT-X-MEDIA per plan. URI now reflects
        // language + variant + codec, so the "English (Full)" / "(Sign)" /
        // "(SDH)" tracks of a single source land as three distinct entries
        // pointing at three distinct files instead of collapsing to one.
        SubtitleOutputPlan[] activeSubs = plan
            .SubtitleOutputs.Where(s => s.Action is StreamAction.Extract or StreamAction.Copy)
            .ToArray();

        if (activeSubs.Length > 0)
        {
            foreach (SubtitleOutputPlan sub in activeSubs)
            {
                string lang = sub.Language ?? "und";
                string displayName = GetSubtitleDisplayName(lang, sub.Variant);
                string subsUri = GetSubtitlePlaylistUri(sub);
                bool isForced = string.Equals(
                    sub.Variant,
                    "sign",
                    StringComparison.OrdinalIgnoreCase
                );

                sb.AppendLine(
                    $"#EXT-X-MEDIA:TYPE=SUBTITLES,GROUP-ID=\"subs\",NAME=\"{displayName}\",LANGUAGE=\"{lang}\",DEFAULT=NO,AUTOSELECT=YES,FORCED={YesNo(isForced)},URI=\"{subsUri}\""
                );
            }

            sb.AppendLine();
        }

        // Video variants with measured bandwidth
        foreach (VideoOutputPlan video in plan.VideoOutputs)
        {
            string codecTag = GetVideoCodecTag(video);
            string audioCodecTag =
                plan.AudioOutputs.Length > 0 ? $",{GetAudioCodecTag(plan.AudioOutputs[0])}" : "";

            // Use measured bandwidth. Apple requires BANDWIDTH = peak, AVERAGE-BANDWIDTH = average.
            // Combine video + audio bandwidth for the STREAM-INF (Apple spec section 4.10).
            HlsVariantAnalyzer.VariantMetrics vidMetrics = videoMetrics.GetValueOrDefault(
                video.MapLabel,
                new(0, 0)
            );
            HlsVariantAnalyzer.VariantMetrics audMetrics =
                plan.AudioOutputs.Length > 0
                    ? audioMetrics.GetValueOrDefault(plan.AudioOutputs[0].MapLabel, new(0, 0))
                    : new(0, 0);

            int peakBandwidth = vidMetrics.PeakBandwidth + audMetrics.PeakBandwidth;
            int avgBandwidth = vidMetrics.AverageBandwidth + audMetrics.AverageBandwidth;

            Dictionary<string, string> tokens = TemplateResolver.VideoTokens(
                video.Width,
                video.Height,
                video.IsHdrOutput
            );
            string playlistResolved = TemplateResolver.Resolve(video.PlaylistNameTemplate, tokens);
            string subDir = StoragePathHelpers.GetParent(playlistResolved) ?? playlistResolved;
            string playlistFile = StoragePathHelpers.GetName(playlistResolved);

            // VIDEO-RANGE labels the colour pipeline (PQ/HLG = HDR transfer,
            // SDR otherwise) — not the bit depth. 10-bit anime / 10-bit BT.709
            // remux is SDR; mislabelling them as PQ makes hls.js reject the
            // master with manifestIncompatibleCodecsError because the segments'
            // actual transfer characteristics don't match.
            string videoRange = video.IsHdrOutput ? "PQ" : "SDR";
            string frameRate = video.FrameRate.ToString("F3", CultureInfo.InvariantCulture);

            string subsAttr =
                activeSubs.Length > 0 ? ",SUBTITLES=\"subs\"" : ",CLOSED-CAPTIONS=NONE";

            sb.AppendLine(
                $"#EXT-X-STREAM-INF:BANDWIDTH={peakBandwidth},AVERAGE-BANDWIDTH={avgBandwidth},RESOLUTION={video.Width}x{video.Height},FRAME-RATE={frameRate},CODECS=\"{codecTag}{audioCodecTag}\",VIDEO-RANGE={videoRange},AUDIO=\"{audioGroupId}\"{subsAttr}"
            );
            sb.AppendLine($"{subDir}/{playlistFile}.m3u8");
        }

        // Emit EXT-X-DATERANGE chapter markers (HLS v8).
        // START-DATE is a UTC ISO 8601 instant derived from epoch + chapter offset,
        // so players can render a chapter timeline without decoding position.
        if (plan.Chapters is { Count: > 0 } chapters)
        {
            DateTimeOffset epoch = DateTimeOffset.UnixEpoch;

            for (int i = 0; i < chapters.Count; i++)
            {
                ChapterInfo chapter = chapters[i];
                double startSeconds = chapter.Start.TotalSeconds;
                double endSeconds =
                    i + 1 < chapters.Count
                        ? chapters[i + 1].Start.TotalSeconds
                        : chapter.End.TotalSeconds;
                double durationSeconds = endSeconds - startSeconds;

                DateTimeOffset startDate = epoch.AddSeconds(startSeconds);
                string startDateIso = startDate.ToString(
                    "yyyy-MM-ddTHH:mm:ss.fffZ",
                    CultureInfo.InvariantCulture
                );
                string title = chapter.Title ?? $"Chapter {i + 1}";
                string escapedTitle = title.Replace("\"", "\\\"");
                string durationFormatted = durationSeconds.ToString(
                    "F3",
                    CultureInfo.InvariantCulture
                );

                sb.AppendLine(
                    $"#EXT-X-DATERANGE:ID=\"ch{i}\",START-DATE=\"{startDateIso}\",DURATION={durationFormatted},X-COM-NOMERCY-CHAPTER-TITLE=\"{escapedTitle}\""
                );
            }
        }

        return sb.ToString();
    }

    private static string GetAudioDisplayName(string language) =>
        Culture.EnglishLanguageName(language);

    private static string GetVideoCodecTag(VideoOutputPlan video) =>
        HlsCodecsStringBuilder.VideoCodecString(
            video.EncoderName,
            video.Profile,
            video.Level,
            video.TenBit
        );

    private static string GetAudioCodecTag(AudioOutputPlan audio) =>
        HlsCodecsStringBuilder.AudioCodecString(audio.EncoderName);

    private static string YesNo(bool value) => value ? "YES" : "NO";

    // ------------------------------------------------------------------
    // Subtitle helpers
    // ------------------------------------------------------------------

    private static string GetSubtitleDisplayName(string language, string variant = "full")
    {
        string langName = Culture.EnglishLanguageName(language);

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
    // for EXT-X-MEDIA. All subtitle assets live under subtitles/ so the
    // root stays clean.
    internal static string GetSubtitlePlaylistUri(SubtitleOutputPlan sub)
    {
        string lang = sub.Language ?? "und";
        string variant = string.IsNullOrEmpty(sub.Variant) ? "full" : sub.Variant;

        return sub.OutputCodec switch
        {
            SubtitleCodecType.Ass => $"subtitles/subs.{lang}.{variant}.ass",
            SubtitleCodecType.Srt => $"subtitles/subs.{lang}.{variant}.srt",
            _ => $"subtitles/subs_{lang}_{variant}.m3u8",
        };
    }

    /// <summary>
    /// Emits the per-subtitle media playlist (.m3u8) referencing the WebVTT
    /// segments produced by <see cref="WebVttSegmenter"/>. Segment filenames
    /// follow subs_&lt;lang&gt;_&lt;variant&gt;_NNNNN.vtt to keep distinct
    /// variants of the same language addressable on disk.
    /// </summary>
    public static string GenerateSubtitleMediaPlaylist(
        SubtitleOutputPlan sub,
        IReadOnlyList<WebVttSegment> segments,
        int segmentDurationSeconds,
        string segmentUriPrefix = ""
    )
    {
        string lang = sub.Language ?? "und";
        string variant = string.IsNullOrEmpty(sub.Variant) ? "full" : sub.Variant;

        StringBuilder sb = new();
        sb.AppendLine("#EXTM3U");
        sb.AppendLine("#EXT-X-VERSION:3");
        sb.AppendLine($"#EXT-X-TARGETDURATION:{segmentDurationSeconds}");
        sb.AppendLine("#EXT-X-PLAYLIST-TYPE:VOD");

        foreach (WebVttSegment seg in segments)
        {
            double actualDuration = (seg.EndTime - seg.StartTime).TotalSeconds;
            string segFile = $"subs_{lang}_{variant}_{seg.Index:D5}.vtt";
            string uri = string.IsNullOrEmpty(segmentUriPrefix)
                ? segFile
                : $"{segmentUriPrefix}/{segFile}";

            sb.AppendLine($"#EXTINF:{actualDuration:F3},");
            sb.AppendLine(uri);
        }

        sb.AppendLine("#EXT-X-ENDLIST");
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
        sb.AppendLine("#EXTM3U");
        sb.AppendLine("#EXT-X-VERSION:3");
        sb.AppendLine($"#EXT-X-TARGETDURATION:{segmentDurationSeconds}");
        sb.AppendLine("#EXT-X-PLAYLIST-TYPE:VOD");
        sb.AppendLine($"#EXTINF:{segmentDurationSeconds}.000,");
        sb.AppendLine(assFileName);
        sb.AppendLine("#EXT-X-ENDLIST");
        return sb.ToString();
    }
}
