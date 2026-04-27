namespace NoMercy.Encoder.Output;

using System.Text;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using NoMercy.Storage;

public class HlsOutputStrategy(IStorage storage) : IOutputStrategy
{
    public OutputFormat Format => OutputFormat.Hls;

    public void ConfigureOutput(
        FfmpegCommandBuilder builder,
        OutputPlan plan,
        string outputDirectory
    )
    {
        int segmentDuration = plan.SegmentDurationSeconds;
        HlsOptions hlsOptions = plan.HlsOptions ?? new HlsOptions();

        // Hoist segment-type derived values; both video and audio loops need them.
        bool isFmp4 = hlsOptions.SegmentType.Equals("fmp4", StringComparison.OrdinalIgnoreCase);
        string segmentExtension = isFmp4 ? ".m4s" : ".ts";

        foreach (VideoOutputPlan video in plan.VideoOutputs)
        {
            Dictionary<string, string> tokens = TemplateResolver.VideoTokens(
                video.Width,
                video.Height,
                video.IsHdrOutput
            );

            // Template resolves to e.g. "video_1920x1080_SDR/video_1920x1080_SDR"
            string segmentResolved = TemplateResolver.Resolve(video.SegmentNameTemplate, tokens);
            string playlistResolved = TemplateResolver.Resolve(video.PlaylistNameTemplate, tokens);

            // Split into directory and filename parts
            string subDir =
                Path.GetDirectoryName(playlistResolved)?.Replace("\\", "/") ?? playlistResolved;
            string playlistFile = Path.GetFileName(playlistResolved);
            string segmentDir =
                Path.GetDirectoryName(segmentResolved)?.Replace("\\", "/") ?? segmentResolved;
            string segmentFile = Path.GetFileName(segmentResolved);

            // Paths are relative — FFmpeg CWD is set to the output directory.
            string playlistPath = $"{subDir}/{playlistFile}.m3u8";

            bool isHevc =
                video.EncoderName.Contains("265", StringComparison.OrdinalIgnoreCase)
                || video.EncoderName.Contains("hevc", StringComparison.OrdinalIgnoreCase);

            int gopCeiling = (int)Math.Ceiling(video.FrameRate * segmentDuration * 2);

            // Build hls_flags: always include independent_segments (existing behaviour).
            // When HlsOptions.IndependentSegments is true the flag is still included —
            // future phases may add additional flags joined with '+' here.
            string hlsFlags = "independent_segments";

            Dictionary<string, string> extraFlags = new(video.ExtraFlags)
            {
                ["-f"] = "hls",
                ["-hls_time"] = segmentDuration.ToString(),
                ["-hls_playlist_type"] = hlsOptions.PlaylistType,
                ["-hls_segment_type"] = hlsOptions.SegmentType,
                ["-hls_flags"] = hlsFlags,
                ["-hls_segment_filename"] = $"{segmentDir}/{segmentFile}_%05d{segmentExtension}",
                ["-force_key_frames"] = $"expr:gte(t,n_forced*{segmentDuration})",
                ["-forced-idr"] = "1",
            };

            // fMP4 requires an init segment with a deterministic name alongside the playlist.
            if (isFmp4)
                extraFlags["-hls_fmp4_init_filename"] = "init.mp4";

            if (isHevc)
                extraFlags["-tag:v"] = "hvc1";

            // Dolby Vision overrides hvc1 — HLS/fMP4 players require dvh1 to
            // route the stream through the DV decoder path.
            if (plan.PreserveDolbyVision && isHevc)
                extraFlags["-tag:v"] = "dvh1";

            builder.AddOutput(
                new(
                    FilePath: playlistPath,
                    VideoCodec: video.EncoderName,
                    Crf: video.Crf > 0 ? video.Crf : null,
                    VideoBitrateKbps: video.BitrateKbps > 0 ? video.BitrateKbps : null,
                    Preset: video.Preset,
                    Profile: video.Profile,
                    Level: video.Level,
                    PixelFormat: video.TenBit ? video.PixelFormat : null,
                    KeyframeInterval: gopCeiling,
                    MapStreams: [video.MapLabel],
                    ExtraFlags: extraFlags
                )
            );
        }

        foreach (AudioOutputPlan audio in plan.AudioOutputs)
        {
            if (audio.Action == StreamAction.Copy || audio.Action == StreamAction.Transcode)
            {
                string codecName = audio.EncoderName.Replace("libfdk_", "").Replace("lib", "");
                Dictionary<string, string> tokens = TemplateResolver.AudioTokens(
                    audio.Language ?? "und",
                    codecName,
                    audio.Channels
                );

                string segmentResolved = TemplateResolver.Resolve(
                    audio.SegmentNameTemplate,
                    tokens
                );
                string playlistResolved = TemplateResolver.Resolve(
                    audio.PlaylistNameTemplate,
                    tokens
                );

                string subDir =
                    Path.GetDirectoryName(playlistResolved)?.Replace("\\", "/") ?? playlistResolved;
                string playlistFile = Path.GetFileName(playlistResolved);
                string segmentDir =
                    Path.GetDirectoryName(segmentResolved)?.Replace("\\", "/") ?? segmentResolved;
                string segmentFile = Path.GetFileName(segmentResolved);

                string playlistPath = $"{subDir}/{playlistFile}.m3u8";

                Dictionary<string, string> extraFlags = new()
                {
                    ["-f"] = "hls",
                    ["-hls_time"] = segmentDuration.ToString(),
                    ["-hls_playlist_type"] = hlsOptions.PlaylistType,
                    ["-hls_segment_type"] = hlsOptions.SegmentType,
                    ["-hls_flags"] = "independent_segments",
                    ["-hls_segment_filename"] =
                        $"{segmentDir}/{segmentFile}_%05d{segmentExtension}",
                };

                if (
                    audio.Action == StreamAction.Transcode
                    && !string.IsNullOrEmpty(audio.AudioFilter)
                )
                {
                    extraFlags["-af"] = audio.AudioFilter;
                }

                string audioCodec = audio.Action == StreamAction.Copy ? "copy" : audio.EncoderName;

                builder.AddOutput(
                    new(
                        FilePath: playlistPath,
                        AudioCodec: audioCodec,
                        AudioBitrateKbps: audio.Action == StreamAction.Transcode
                            ? audio.BitrateKbps
                            : null,
                        AudioChannels: audio.Channels.ToString(),
                        AudioSampleRate: audio.SampleRate,
                        MapStreams: [audio.MapLabel],
                        ExtraFlags: extraFlags
                    )
                );
            }
        }
    }

    public async Task FinalizeAsync(
        string outputDirectory,
        OutputPlan plan,
        string mediaTitle,
        CancellationToken ct
    )
    {
        // Measure actual bitrates from the encoded variant playlists.
        // These are the real values — not estimates from profile settings.
        HlsVariantAnalyzer analyzer = new(storage);
        Dictionary<string, HlsVariantAnalyzer.VariantMetrics> videoMetrics = [];
        foreach (VideoOutputPlan video in plan.VideoOutputs)
        {
            Dictionary<string, string> tokens = TemplateResolver.VideoTokens(
                video.Width,
                video.Height,
                video.IsHdrOutput
            );
            string playlistResolved = TemplateResolver.Resolve(video.PlaylistNameTemplate, tokens);
            string subDir =
                Path.GetDirectoryName(playlistResolved)?.Replace("\\", "/") ?? playlistResolved;
            string playlistFile = Path.GetFileName(playlistResolved);
            string variantPath = Path.Combine(outputDirectory, subDir, $"{playlistFile}.m3u8");

            videoMetrics[video.MapLabel] = analyzer.Measure(variantPath);
        }

        Dictionary<string, HlsVariantAnalyzer.VariantMetrics> audioMetrics = [];
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
            string variantPath = Path.Combine(outputDirectory, subDir, $"{playlistFile}.m3u8");

            audioMetrics[audio.MapLabel] = analyzer.Measure(variantPath);
        }

        PlaylistGenerator generator = new();
        string masterPlaylist = generator.GenerateMasterPlaylist(
            plan,
            mediaTitle,
            videoMetrics,
            audioMetrics
        );
        string masterPath = Path.Combine(outputDirectory, $"{mediaTitle}.m3u8");
        await storage.WriteAsync(masterPath, Encoding.UTF8.GetBytes(masterPlaylist), ct);

        await WriteSubtitleSidecarsAsync(outputDirectory, plan, ct);
    }

    // Wraps WebVTT extracts in single-segment VOD playlists the master can
    // reference. ASS/SRT URIs in the master point straight at the file, so
    // those plans skip this path entirely.
    private async Task WriteSubtitleSidecarsAsync(
        string outputDirectory,
        OutputPlan plan,
        CancellationToken ct
    )
    {
        SubtitleOutputPlan[] webVttSubs = plan
            .SubtitleOutputs.Where(s =>
                s.Action is StreamAction.Extract or StreamAction.Copy
                && s.OutputCodec is SubtitleCodecType.WebVtt
            )
            .ToArray();
        if (webVttSubs.Length == 0)
            return;

        string subtitlesDir = Path.Combine(outputDirectory, "subtitles");
        if (!storage.Exists(subtitlesDir))
            return;

        string[] vttFiles = storage
            .List(subtitlesDir, "*.vtt", recursive: false)
            .Where(e => !e.IsDirectory)
            .Select(e => e.Path)
            .ToArray();
        if (vttFiles.Length == 0)
            return;

        double durationSeconds = MeasureVideoDuration(outputDirectory, plan);
        if (durationSeconds <= 0)
            durationSeconds = 36000;

        foreach (SubtitleOutputPlan sub in webVttSubs)
        {
            string lang = sub.Language ?? "und";
            string variant = string.IsNullOrEmpty(sub.Variant) ? "full" : sub.Variant;
            string sidecarFile = $"subs_{lang}_{variant}.m3u8";

            // Match the .vtt file the extractor produced via the same
            // template tokens (subs.<lang>.<variant>.vtt); fall back to a
            // language-only match if the variant token isn't in the name.
            string? vttPath = vttFiles.FirstOrDefault(f =>
                Path.GetFileName(f)
                    .Contains($".{lang}.{variant}.", StringComparison.OrdinalIgnoreCase)
            );
            vttPath ??= vttFiles.FirstOrDefault(f =>
                Path.GetFileName(f).Contains($".{lang}.", StringComparison.OrdinalIgnoreCase)
            );
            if (vttPath is null)
                continue;

            // Sidecar lives next to the .vtt it wraps (subtitles/), so the
            // master URI subtitles/subs_<lang>_<variant>.m3u8 resolves to a
            // playlist whose own EXTINF can use a bare filename.
            string vttFile = Path.GetFileName(vttPath);
            string playlist =
                "#EXTM3U\n"
                + "#EXT-X-VERSION:6\n"
                + "#EXT-X-PLAYLIST-TYPE:VOD\n"
                + $"#EXT-X-TARGETDURATION:{(int)Math.Ceiling(durationSeconds)}\n"
                + $"#EXTINF:{durationSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)},\n"
                + vttFile
                + "\n#EXT-X-ENDLIST\n";

            string sidecarPath = Path.Combine(subtitlesDir, sidecarFile);
            await storage.WriteAsync(sidecarPath, Encoding.UTF8.GetBytes(playlist), ct);
        }
    }

    /// <summary>
    /// Sums #EXTINF durations from the first video variant playlist as the
    /// authoritative content length. Falls back to 0 if no variant is found.
    /// </summary>
    private double MeasureVideoDuration(string outputDirectory, OutputPlan plan)
    {
        VideoOutputPlan? firstVideo = plan.VideoOutputs.FirstOrDefault();
        if (firstVideo is null)
            return 0;

        Dictionary<string, string> tokens = TemplateResolver.VideoTokens(
            firstVideo.Width,
            firstVideo.Height,
            firstVideo.IsHdrOutput
        );
        string resolved = TemplateResolver.Resolve(firstVideo.PlaylistNameTemplate, tokens);
        string subDir = Path.GetDirectoryName(resolved)?.Replace("\\", "/") ?? resolved;
        string playlistFile = Path.GetFileName(resolved);
        string variantPath = Path.Combine(outputDirectory, subDir, $"{playlistFile}.m3u8");

        if (!storage.Exists(variantPath))
            return 0;

        string content = Encoding.UTF8.GetString(storage.Read(variantPath));
        double total = 0;
        foreach (string line in content.Split('\n'))
        {
            if (!line.StartsWith("#EXTINF:", StringComparison.Ordinal))
                continue;
            int comma = line.IndexOf(',');
            string value = comma > 8 ? line[8..comma] : line[8..];
            if (
                double.TryParse(
                    value.Trim(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double seconds
                )
            )
                total += seconds;
        }
        return total;
    }

    public string[] GetOutputSubdirectories(OutputPlan plan)
    {
        List<string> dirs = [];

        foreach (VideoOutputPlan video in plan.VideoOutputs)
        {
            Dictionary<string, string> tokens = TemplateResolver.VideoTokens(
                video.Width,
                video.Height,
                video.IsHdrOutput
            );
            string resolved = TemplateResolver.Resolve(video.PlaylistNameTemplate, tokens);
            string subDir = Path.GetDirectoryName(resolved)?.Replace("\\", "/") ?? resolved;
            dirs.Add(subDir);
        }

        foreach (AudioOutputPlan audio in plan.AudioOutputs)
        {
            if (audio.Action is StreamAction.Copy or StreamAction.Transcode)
            {
                string codecName = audio.EncoderName.Replace("libfdk_", "").Replace("lib", "");
                Dictionary<string, string> tokens = TemplateResolver.AudioTokens(
                    audio.Language ?? "und",
                    codecName,
                    audio.Channels
                );
                string resolved = TemplateResolver.Resolve(audio.PlaylistNameTemplate, tokens);
                string subDir = Path.GetDirectoryName(resolved)?.Replace("\\", "/") ?? resolved;
                dirs.Add(subDir);
            }
        }

        return dirs.ToArray();
    }
}
