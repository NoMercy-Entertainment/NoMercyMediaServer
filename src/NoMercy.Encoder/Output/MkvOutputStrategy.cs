namespace NoMercy.Encoder.Output;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Pipeline;

public class MkvOutputStrategy : IOutputStrategy
{
    public OutputFormat Format => OutputFormat.Mkv;

    public void ConfigureOutput(
        FfmpegCommandBuilder builder,
        OutputPlan plan,
        string outputDirectory
    )
    {
        string outputPath = Path.Combine(outputDirectory, "output.mkv");
        List<string> mapStreams = [];

        foreach (VideoOutputPlan video in plan.VideoOutputs)
            mapStreams.Add(video.MapLabel);

        foreach (AudioOutputPlan audio in plan.AudioOutputs)
            if (audio.Action is StreamAction.Copy or StreamAction.Transcode)
                mapStreams.Add(audio.MapLabel);

        foreach (SubtitleOutputPlan sub in plan.SubtitleOutputs)
            if (sub.Action is StreamAction.Copy)
                mapStreams.Add(sub.MapLabel ?? $"0:s:{sub.SourceIndex}");

        VideoOutputPlan? primaryVideo = plan.VideoOutputs.Length > 0 ? plan.VideoOutputs[0] : null;
        AudioOutputPlan? primaryAudio = plan.AudioOutputs.Length > 0 ? plan.AudioOutputs[0] : null;

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
                MapStreams: mapStreams.ToArray()
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
        // Rename the generic output.mkv to the proper media title
        string sourcePath = Path.Combine(outputDirectory, "output.mkv");
        string targetPath = Path.Combine(outputDirectory, $"{mediaTitle}.mkv");

        if (File.Exists(sourcePath) && sourcePath != targetPath)
            File.Move(sourcePath, targetPath, overwrite: true);

        return Task.CompletedTask;
    }

    public string[] GetOutputSubdirectories(OutputPlan plan) => [];
}
