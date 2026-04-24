namespace NoMercy.Encoder.Output;

using System.Globalization;
using System.Text;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;

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
        HlsOptions hlsOptions = plan.HlsOptions ?? new HlsOptions();

        bool hasFmp4 = hlsOptions.SegmentType.Equals("fmp4", StringComparison.OrdinalIgnoreCase);
        bool hasSubsGroup = plan.SubtitleOutputs.Any(s =>
            s.Action is StreamAction.Extract or StreamAction.Copy
        );

        // hasChapterDateRanges is wired by Phase 4.5; default false until then.
        bool hasChapterDateRanges = false;

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
            string subDir =
                Path.GetDirectoryName(playlistResolved)?.Replace("\\", "/") ?? playlistResolved;
            string playlistFile = Path.GetFileName(playlistResolved);

            string uri = $"{subDir}/{playlistFile}.m3u8";
            string language = audio.Language ?? "und";
            string displayName = GetAudioDisplayName(language);
            bool isDefault = audio == plan.AudioOutputs[0];

            sb.AppendLine(
                $"#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=\"{audioGroupId}\",LANGUAGE=\"{language}\",AUTOSELECT=YES,DEFAULT={YesNo(isDefault)},URI=\"{uri}\",NAME=\"{displayName}\""
            );
        }

        sb.AppendLine();

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
                video.TenBit
            );
            string playlistResolved = TemplateResolver.Resolve(video.PlaylistNameTemplate, tokens);
            string subDir =
                Path.GetDirectoryName(playlistResolved)?.Replace("\\", "/") ?? playlistResolved;
            string playlistFile = Path.GetFileName(playlistResolved);

            string videoRange = video.TenBit ? "PQ" : "SDR";
            string frameRate = video.FrameRate.ToString("F3", CultureInfo.InvariantCulture);

            sb.AppendLine(
                $"#EXT-X-STREAM-INF:BANDWIDTH={peakBandwidth},AVERAGE-BANDWIDTH={avgBandwidth},RESOLUTION={video.Width}x{video.Height},FRAME-RATE={frameRate},CODECS=\"{codecTag}{audioCodecTag}\",VIDEO-RANGE={videoRange},AUDIO=\"{audioGroupId}\""
            );
            sb.AppendLine($"{subDir}/{playlistFile}.m3u8");
        }

        return sb.ToString();
    }

    private static string GetAudioDisplayName(string language)
    {
        return language.ToUpperInvariant() switch
        {
            "ENG" => "English",
            "FRE" or "FRA" => "French",
            "GER" or "DEU" => "German",
            "SPA" => "Spanish",
            "ITA" => "Italian",
            "DUT" or "NLD" => "Dutch",
            "JPN" or "JAP" => "Japanese",
            "KOR" => "Korean",
            "CHI" or "ZHO" => "Chinese",
            "RUS" => "Russian",
            "POR" => "Portuguese",
            "ARA" => "Arabic",
            "HIN" => "Hindi",
            "SWE" => "Swedish",
            "NOR" => "Norwegian",
            "DAN" => "Danish",
            "FIN" => "Finnish",
            "POL" => "Polish",
            "TUR" => "Turkish",
            "UND" => "Unknown",
            _ => language,
        };
    }

    private static string GetVideoCodecTag(VideoOutputPlan video)
    {
        string encoder = video.EncoderName.ToLowerInvariant();

        if (encoder.Contains("264") || encoder.Contains("x264"))
        {
            return video.Level switch
            {
                "4.0" => "avc1.640028",
                "4.1" => "avc1.640029",
                "5.0" => "avc1.640032",
                "5.1" => "avc1.640033",
                _ => "avc1.640028",
            };
        }

        if (encoder.Contains("265") || encoder.Contains("hevc"))
            return video.TenBit ? "hvc1.2.4.L153.B0" : "hvc1.1.6.L93.B0";

        if (encoder.Contains("av1") || encoder.Contains("svtav1") || encoder.Contains("aom"))
            return video.TenBit ? "av01.0.15M.10" : "av01.0.15M.08";

        return "avc1.640028";
    }

    private static string GetAudioCodecTag(AudioOutputPlan audio)
    {
        return audio.EncoderName.ToLowerInvariant() switch
        {
            "aac" or "libfdk_aac" => "mp4a.40.2",
            "ac3" => "ac-3",
            "eac3" => "ec-3",
            "libopus" or "opus" => "opus",
            _ => "mp4a.40.2",
        };
    }

    private static string YesNo(bool value) => value ? "YES" : "NO";
}
