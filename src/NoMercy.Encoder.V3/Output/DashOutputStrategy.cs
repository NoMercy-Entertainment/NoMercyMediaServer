namespace NoMercy.Encoder.V3.Output;

using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Commands;
using NoMercy.Encoder.V3.Pipeline;

public class DashOutputStrategy : IOutputStrategy
{
    public OutputFormat Format => OutputFormat.Dash;

    public int SegmentDurationSeconds { get; init; } = 6;

    public void ConfigureOutput(
        FfmpegCommandBuilder builder,
        OutputPlan plan,
        string outputDirectory
    )
    {
        string mpdPath = Path.Combine(outputDirectory, "manifest.mpd");
        List<string> mapStreams = [];

        foreach (VideoOutputPlan video in plan.VideoOutputs)
            mapStreams.Add(video.MapLabel);

        foreach (AudioOutputPlan audio in plan.AudioOutputs)
            if (audio.Action is StreamAction.Copy or StreamAction.Transcode)
                mapStreams.Add(audio.MapLabel);

        VideoOutputPlan? primaryVideo = plan.VideoOutputs.Length > 0 ? plan.VideoOutputs[0] : null;
        AudioOutputPlan? primaryAudio = plan.AudioOutputs.Length > 0 ? plan.AudioOutputs[0] : null;

        Dictionary<string, string> extraFlags = new()
        {
            ["-f"] = "dash",
            ["-seg_duration"] = SegmentDurationSeconds.ToString(),
            ["-init_seg_name"] = "init_$RepresentationID$.m4s",
            ["-media_seg_name"] = "seg_$RepresentationID$_$Number%05d$.m4s",
            ["-use_template"] = "1",
            ["-use_timeline"] = "1",
            ["-adaptation_sets"] = "id=0,streams=v id=1,streams=a",
        };

        builder.AddOutput(
            new OutputOptions(
                FilePath: mpdPath,
                VideoCodec: primaryVideo?.EncoderName,
                AudioCodec: primaryAudio?.Action == StreamAction.Copy
                    ? "copy"
                    : primaryAudio?.EncoderName,
                Crf: primaryVideo is { Crf: > 0 } ? primaryVideo.Crf : null,
                VideoBitrateKbps: primaryVideo is { BitrateKbps: > 0 }
                    ? primaryVideo.BitrateKbps
                    : null,
                Preset: primaryVideo?.Preset,
                Profile: primaryVideo?.Profile,
                PixelFormat: primaryVideo is { TenBit: true } ? primaryVideo.PixelFormat : null,
                AudioBitrateKbps: primaryAudio?.Action == StreamAction.Transcode
                    ? primaryAudio.BitrateKbps
                    : null,
                MapStreams: mapStreams.ToArray(),
                ExtraFlags: extraFlags
            )
        );
    }

    public Task FinalizeAsync(string outputDirectory, OutputPlan plan, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public string[] GetOutputSubdirectories(OutputPlan plan) => [];
}
