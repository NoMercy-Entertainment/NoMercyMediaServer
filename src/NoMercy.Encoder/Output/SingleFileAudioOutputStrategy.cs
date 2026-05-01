using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Pipeline;
using NoMercy.Storage;

namespace NoMercy.Encoder.Output;

/// <summary>
/// Shared pipeline for single-file audio-only containers (mp3 / flac / ogg).
/// Each subclass only differs by file extension, FFmpeg output format token,
/// and the codec the container mandates.
///
/// Contract: exactly one audio output, zero video outputs. Anything else
/// falls through to the first audio output and ignores the rest — the
/// <see cref="NoMercy.Encoder.Profiles.ProfileValidator"/> catches this
/// earlier so we never land here with unsupported input.
/// </summary>
public abstract class SingleFileAudioOutputStrategy(IStorage storage) : IOutputStrategy
{
    public abstract OutputFormat Format { get; }

    /// <summary>File extension including the leading dot (e.g. <c>.mp3</c>).</summary>
    protected abstract string Extension { get; }

    /// <summary>
    /// Value for ffmpeg's <c>-f</c> muxer selection. Needed because some
    /// containers (ogg) don't always auto-detect from extension alone when
    /// the output path is being piped or renamed post-encode.
    /// </summary>
    protected abstract string MuxerName { get; }

    /// <summary>
    /// Optional codec override applied when the resolved audio codec doesn't
    /// match what this container accepts. <c>null</c> means trust the planner.
    /// </summary>
    protected virtual string? ForcedCodec => null;

    public void ConfigureOutput(
        FfmpegCommandBuilder builder,
        OutputPlan plan,
        string outputDirectory
    )
    {
        string outputPath = Path.Combine(outputDirectory, $"output{Extension}");

        AudioOutputPlan? audio = plan.AudioOutputs.Length > 0 ? plan.AudioOutputs[0] : null;
        string? audioCodec = ResolveAudioCodec(audio);

        List<string> mapStreams = [];
        if (audio is not null)
            mapStreams.Add(audio.MapLabel);

        Dictionary<string, string> extraFlags = new() { ["-f"] = MuxerName };

        if (audio is { Action: StreamAction.Transcode } && !string.IsNullOrEmpty(audio.AudioFilter))
        {
            extraFlags["-af"] = audio.AudioFilter;
        }

        builder.AddOutput(
            new(
                FilePath: outputPath,
                AudioCodec: audioCodec,
                AudioBitrateKbps: audio?.Action == StreamAction.Transcode
                    ? audio.BitrateKbps
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
        string sourcePath = Path.Combine(outputDirectory, $"output{Extension}");
        string targetPath = Path.Combine(outputDirectory, $"{mediaTitle}{Extension}");

        if (storage.Exists(sourcePath) && sourcePath != targetPath)
        {
            storage.Delete(targetPath);
            storage.Move(sourcePath, targetPath);
        }

        return Task.CompletedTask;
    }

    public string[] GetOutputSubdirectories(OutputPlan plan) => [];

    private string? ResolveAudioCodec(AudioOutputPlan? audio)
    {
        if (audio is null)
            return null;
        if (audio.Action == StreamAction.Copy)
            return "copy";
        return ForcedCodec ?? audio.EncoderName;
    }
}

public class Mp3OutputStrategy(IStorage storage) : SingleFileAudioOutputStrategy(storage)
{
    public override OutputFormat Format => OutputFormat.Mp3;
    protected override string Extension => ".mp3";
    protected override string MuxerName => "mp3";

    // MP3 container rejects everything except libmp3lame. If a profile asks
    // for AAC into .mp3, that's a bug — force lame and let the validator
    // surface it next run.
    protected override string? ForcedCodec => "libmp3lame";
}

public class FlacOutputStrategy(IStorage storage) : SingleFileAudioOutputStrategy(storage)
{
    public override OutputFormat Format => OutputFormat.Flac;
    protected override string Extension => ".flac";
    protected override string MuxerName => "flac";
    protected override string? ForcedCodec => "flac";
}

public class OggOutputStrategy(IStorage storage) : SingleFileAudioOutputStrategy(storage)
{
    public override OutputFormat Format => OutputFormat.Ogg;
    protected override string Extension => ".ogg";
    protected override string MuxerName => "ogg";

    // Ogg accepts vorbis, opus, flac. We leave the planner's choice alone —
    // libvorbis is the default per ProfileMapper, libopus / flac are both
    // valid if the profile explicitly selects them.
}
