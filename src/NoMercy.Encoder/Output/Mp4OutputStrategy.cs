namespace NoMercy.Encoder.Output;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Pipeline;

public class Mp4OutputStrategy : IOutputStrategy
{
    public OutputFormat Format => OutputFormat.Mp4;

    public void ConfigureOutput(
        FfmpegCommandBuilder builder,
        OutputPlan plan,
        string outputDirectory
    )
    {
        string outputPath = Path.Combine(outputDirectory, "output.mp4");
        List<string> mapStreams = [];

        foreach (VideoOutputPlan video in plan.VideoOutputs)
            mapStreams.Add(video.MapLabel);

        foreach (AudioOutputPlan audio in plan.AudioOutputs)
            if (audio.Action is StreamAction.Copy or StreamAction.Transcode)
                mapStreams.Add(audio.MapLabel);

        VideoOutputPlan? primaryVideo = plan.VideoOutputs.Length > 0 ? plan.VideoOutputs[0] : null;
        AudioOutputPlan? primaryAudio = plan.AudioOutputs.Length > 0 ? plan.AudioOutputs[0] : null;

        Dictionary<string, string> extraFlags = new() { ["-movflags"] = "+faststart" };

        if (
            primaryAudio?.Action == StreamAction.Transcode
            && !string.IsNullOrEmpty(primaryAudio.AudioFilter)
        )
        {
            extraFlags["-af"] = primaryAudio.AudioFilter;
        }

        builder.AddOutput(
            new OutputOptions(
                FilePath: outputPath,
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

    public Task FinalizeAsync(
        string outputDirectory,
        OutputPlan plan,
        string mediaTitle,
        CancellationToken ct
    )
    {
        // Rename the generic output.mp4 to the proper media title. Audio-only
        // encodes land as .m4a (matches V1 music-encode behavior and the
        // convention music players expect); video-bearing encodes stay .mp4.
        string extension = plan.VideoOutputs.Length == 0 ? ".m4a" : ".mp4";
        string sourcePath = Path.Combine(outputDirectory, "output.mp4");
        string targetPath = Path.Combine(outputDirectory, $"{mediaTitle}{extension}");

        if (File.Exists(sourcePath) && sourcePath != targetPath)
            File.Move(sourcePath, targetPath, overwrite: true);

        return Task.CompletedTask;
    }

    public string[] GetOutputSubdirectories(OutputPlan plan) => [];
}
