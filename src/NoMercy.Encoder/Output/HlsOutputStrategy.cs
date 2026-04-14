namespace NoMercy.Encoder.Output;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Pipeline;

public class HlsOutputStrategy : IOutputStrategy
{
    public OutputFormat Format => OutputFormat.Hls;

    public int SegmentDurationSeconds { get; init; } = 6;

    public void ConfigureOutput(
        FfmpegCommandBuilder builder,
        OutputPlan plan,
        string outputDirectory
    )
    {
        foreach (VideoOutputPlan video in plan.VideoOutputs)
        {
            Dictionary<string, string> tokens = TemplateResolver.VideoTokens(
                video.Width,
                video.Height,
                video.TenBit
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

            string playlistPath = Path.Combine(outputDirectory, subDir, $"{playlistFile}.m3u8");

            bool isHevc =
                video.EncoderName.Contains("265", StringComparison.OrdinalIgnoreCase)
                || video.EncoderName.Contains("hevc", StringComparison.OrdinalIgnoreCase);

            Dictionary<string, string> extraFlags = new(video.ExtraFlags)
            {
                ["-f"] = "hls",
                ["-hls_time"] = SegmentDurationSeconds.ToString(),
                ["-hls_playlist_type"] = "vod",
                ["-hls_flags"] = "independent_segments",
                ["-hls_segment_filename"] = Path.Combine(
                    outputDirectory,
                    segmentDir,
                    $"{segmentFile}_%05d.ts"
                ),
            };

            if (isHevc)
                extraFlags["-tag:v"] = "hvc1";

            builder.AddOutput(
                new OutputOptions(
                    FilePath: playlistPath,
                    VideoCodec: video.EncoderName,
                    Crf: video.Crf > 0 ? video.Crf : null,
                    VideoBitrateKbps: video.BitrateKbps > 0 ? video.BitrateKbps : null,
                    Preset: video.Preset,
                    Profile: video.Profile,
                    Level: video.Level,
                    PixelFormat: video.TenBit ? video.PixelFormat : null,
                    KeyframeInterval: SegmentDurationSeconds * 30,
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

                string playlistPath = Path.Combine(outputDirectory, subDir, $"{playlistFile}.m3u8");

                Dictionary<string, string> extraFlags = new()
                {
                    ["-f"] = "hls",
                    ["-hls_time"] = SegmentDurationSeconds.ToString(),
                    ["-hls_playlist_type"] = "vod",
                    ["-hls_flags"] = "independent_segments",
                    ["-hls_segment_filename"] = Path.Combine(
                        outputDirectory,
                        segmentDir,
                        $"{segmentFile}_%05d.ts"
                    ),
                };

                string audioCodec = audio.Action == StreamAction.Copy ? "copy" : audio.EncoderName;

                builder.AddOutput(
                    new OutputOptions(
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
        PlaylistGenerator generator = new();
        string masterPlaylist = generator.GenerateMasterPlaylist(plan, mediaTitle);
        string masterPath = Path.Combine(outputDirectory, $"{mediaTitle}.m3u8");
        await File.WriteAllTextAsync(masterPath, masterPlaylist, ct);
    }

    public string[] GetOutputSubdirectories(OutputPlan plan)
    {
        List<string> dirs = [];

        foreach (VideoOutputPlan video in plan.VideoOutputs)
        {
            Dictionary<string, string> tokens = TemplateResolver.VideoTokens(
                video.Width,
                video.Height,
                video.TenBit
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
