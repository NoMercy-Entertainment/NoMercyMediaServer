namespace NoMercy.Encoder.V3.Output;

using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Commands;
using NoMercy.Encoder.V3.Pipeline;

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
            string subDir = $"video_{video.Width}x{video.Height}";
            string playlistPath = Path.Combine(outputDirectory, subDir, $"{subDir}.m3u8");

            Dictionary<string, string> extraFlags = new(video.ExtraFlags)
            {
                ["-f"] = "hls",
                ["-hls_time"] = SegmentDurationSeconds.ToString(),
                ["-hls_playlist_type"] = "vod",
                ["-hls_flags"] = "independent_segments",
                ["-hls_segment_type"] = "fmp4",
                ["-hls_segment_filename"] = Path.Combine(outputDirectory, subDir, "seg_%05d.m4s"),
                ["-hls_fmp4_init_filename"] = "init.mp4",
            };

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
                string subDir = $"audio_{audio.Language ?? "und"}_{audio.Channels}ch";
                string playlistPath = Path.Combine(outputDirectory, subDir, $"{subDir}.m3u8");

                Dictionary<string, string> extraFlags = new()
                {
                    ["-f"] = "hls",
                    ["-hls_time"] = SegmentDurationSeconds.ToString(),
                    ["-hls_playlist_type"] = "vod",
                    ["-hls_flags"] = "independent_segments",
                    ["-hls_segment_type"] = "fmp4",
                    ["-hls_segment_filename"] = Path.Combine(
                        outputDirectory,
                        subDir,
                        "seg_%05d.m4s"
                    ),
                    ["-hls_fmp4_init_filename"] = "init.mp4",
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

    public async Task FinalizeAsync(string outputDirectory, OutputPlan plan, CancellationToken ct)
    {
        PlaylistGenerator generator = new();
        string masterPlaylist = generator.GenerateMasterPlaylist(plan);
        string masterPath = Path.Combine(outputDirectory, "master.m3u8");
        await File.WriteAllTextAsync(masterPath, masterPlaylist, ct);
    }

    public string[] GetOutputSubdirectories(OutputPlan plan)
    {
        List<string> dirs = [];

        foreach (VideoOutputPlan video in plan.VideoOutputs)
            dirs.Add($"video_{video.Width}x{video.Height}");

        foreach (AudioOutputPlan audio in plan.AudioOutputs)
            if (audio.Action is StreamAction.Copy or StreamAction.Transcode)
                dirs.Add($"audio_{audio.Language ?? "und"}_{audio.Channels}ch");

        return dirs.ToArray();
    }
}
