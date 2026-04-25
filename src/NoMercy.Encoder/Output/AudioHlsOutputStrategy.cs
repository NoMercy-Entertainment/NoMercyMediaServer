namespace NoMercy.Encoder.Output;

using System.Text;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Pipeline;
using NoMercy.Storage;

/// <summary>
/// Audio-only HLS output. Produces <c>audio.m3u8</c> and
/// <c>audio_NNN.aac</c> segments — no video, no subtitles, no master
/// playlist. The music player (hls.js) loads <c>audio.m3u8</c> directly.
/// </summary>
public class AudioHlsOutputStrategy(IStorage storage) : IOutputStrategy
{
    public OutputFormat Format => OutputFormat.AudioHls;

    private const int DefaultSegmentSeconds = 6;
    private const string DefaultAudioCodec = "aac";
    private const string DefaultAacProfile = "aac_low";

    public void ConfigureOutput(
        FfmpegCommandBuilder builder,
        OutputPlan plan,
        string outputDirectory
    )
    {
        int segmentDuration =
            plan.SegmentDurationSeconds > 0 ? plan.SegmentDurationSeconds : DefaultSegmentSeconds;

        AudioOutputPlan? audio = plan.AudioOutputs.Length > 0 ? plan.AudioOutputs[0] : null;

        string audioCodec =
            audio?.Action == StreamAction.Copy ? "copy"
            : !string.IsNullOrEmpty(audio?.EncoderName) ? audio.EncoderName
            : DefaultAudioCodec;

        string playlistPath = "audio.m3u8";
        string segmentPattern = "audio_%03d.aac";

        Dictionary<string, string> extraFlags = new()
        {
            ["-f"] = "hls",
            ["-hls_time"] = segmentDuration.ToString(),
            ["-hls_playlist_type"] = "vod",
            ["-hls_flags"] = "independent_segments",
            ["-hls_segment_filename"] = segmentPattern,
        };

        // Force AAC LC profile for broadest HLS compatibility when transcoding.
        if (audioCodec != "copy")
            extraFlags["-profile:a"] = DefaultAacProfile;

        if (audio is { Action: StreamAction.Transcode } && !string.IsNullOrEmpty(audio.AudioFilter))
            extraFlags["-af"] = audio.AudioFilter;

        List<string> mapStreams = [];
        if (audio is not null)
            mapStreams.Add(audio.MapLabel);

        builder.AddOutput(
            new(
                FilePath: playlistPath,
                AudioCodec: audioCodec,
                AudioBitrateKbps: audio?.Action == StreamAction.Transcode
                    ? (audio.BitrateKbps > 0 ? audio.BitrateKbps : 128)
                    : null,
                AudioChannels: audio is not null ? audio.Channels.ToString() : "2",
                AudioSampleRate: audio?.SampleRate,
                MapStreams: mapStreams.ToArray(),
                ExtraFlags: extraFlags
            )
        );
    }

    public async Task FinalizeAsync(
        string outputDirectory,
        OutputPlan plan,
        string mediaTitle,
        CancellationToken ct
    )
    {
        // Audio HLS playlists are self-contained — no master playlist needed.
        // The music player loads audio.m3u8 directly. Nothing to rename.
        await Task.CompletedTask;
    }

    public string[] GetOutputSubdirectories(OutputPlan plan) => [];
}
