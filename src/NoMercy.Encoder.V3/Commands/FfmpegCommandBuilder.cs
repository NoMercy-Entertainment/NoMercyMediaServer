namespace NoMercy.Encoder.V3.Commands;

using System.Globalization;

public class FfmpegCommandBuilder
{
    private GlobalOptions _globalOptions = new();
    private readonly List<InputOptions> _inputs = [];
    private string? _filterComplex;
    private readonly List<OutputOptions> _outputs = [];

    public FfmpegCommandBuilder WithGlobalOptions(GlobalOptions options)
    {
        _globalOptions = options;
        return this;
    }

    public FfmpegCommandBuilder AddInput(InputOptions input)
    {
        _inputs.Add(input);
        return this;
    }

    public FfmpegCommandBuilder WithFilterComplex(string filterGraph)
    {
        _filterComplex = filterGraph;
        return this;
    }

    public FfmpegCommandBuilder AddOutput(OutputOptions output)
    {
        _outputs.Add(output);
        return this;
    }

    public FfmpegCommand Build(string ffmpegPath, string? workingDirectory = null)
    {
        List<string> args = [];

        // Global options
        if (_globalOptions.Overwrite)
            args.Add("-y");
        if (_globalOptions.HideBanner)
            args.Add("-hide_banner");
        if (_globalOptions.ProgressPipe)
        {
            args.Add("-progress");
            args.Add("pipe:1");
        }
        if (_globalOptions.Threads.HasValue)
        {
            args.Add("-threads");
            args.Add(_globalOptions.Threads.Value.ToString());
        }
        if (_globalOptions.ProbeSizeBytes.HasValue)
        {
            args.Add("-probesize");
            args.Add(_globalOptions.ProbeSizeBytes.Value.ToString());
        }
        if (_globalOptions.AnalyzeDurationUs.HasValue)
        {
            args.Add("-analyzeduration");
            args.Add(_globalOptions.AnalyzeDurationUs.Value.ToString());
        }

        // Inputs
        foreach (InputOptions input in _inputs)
        {
            if (input.HwAccelDevice is not null)
            {
                args.Add("-hwaccel");
                args.Add(input.HwAccelDevice);
            }
            if (input.HwAccelOutputFormat is not null)
            {
                args.Add("-hwaccel_output_format");
                args.Add(input.HwAccelOutputFormat);
            }
            if (input.SeekTo.HasValue)
            {
                args.Add("-ss");
                args.Add(
                    input.SeekTo.Value.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)
                );
            }
            if (input.Duration.HasValue)
            {
                args.Add("-t");
                args.Add(
                    input.Duration.Value.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)
                );
            }
            args.Add("-i");
            args.Add(input.FilePath);
        }

        // Filter complex
        if (_filterComplex is not null)
        {
            args.Add("-filter_complex");
            args.Add(_filterComplex);
        }

        // Outputs
        foreach (OutputOptions output in _outputs)
        {
            foreach (string map in output.MapStreams ?? [])
            {
                args.Add("-map");
                args.Add(map);
            }
            if (output.VideoCodec is not null)
            {
                args.Add("-c:v");
                args.Add(output.VideoCodec);
            }
            if (output.AudioCodec is not null)
            {
                args.Add("-c:a");
                args.Add(output.AudioCodec);
            }
            if (output.SubtitleCodec is not null)
            {
                args.Add("-c:s");
                args.Add(output.SubtitleCodec);
            }
            if (output.Preset is not null)
            {
                args.Add("-preset");
                args.Add(output.Preset);
            }
            if (output.Profile is not null)
            {
                args.Add("-profile:v");
                args.Add(output.Profile);
            }
            if (output.Level is not null)
            {
                args.Add("-level");
                args.Add(output.Level);
            }
            if (output.PixelFormat is not null)
            {
                args.Add("-pix_fmt");
                args.Add(output.PixelFormat);
            }
            if (output.Crf.HasValue)
            {
                args.Add("-crf");
                args.Add(output.Crf.Value.ToString());
            }
            if (output.VideoBitrateKbps.HasValue)
            {
                args.Add("-b:v");
                args.Add($"{output.VideoBitrateKbps.Value}k");
            }
            if (output.AudioBitrateKbps.HasValue)
            {
                args.Add("-b:a");
                args.Add($"{output.AudioBitrateKbps.Value}k");
            }
            if (output.AudioChannels is not null)
            {
                args.Add("-ac");
                args.Add(output.AudioChannels);
            }
            if (output.AudioSampleRate.HasValue)
            {
                args.Add("-ar");
                args.Add(output.AudioSampleRate.Value.ToString());
            }
            if (output.KeyframeInterval.HasValue)
            {
                args.Add("-g");
                args.Add(output.KeyframeInterval.Value.ToString());
            }
            if (output.ExtraFlags is not null)
            {
                foreach (KeyValuePair<string, string> flag in output.ExtraFlags)
                {
                    args.Add(flag.Key);
                    args.Add(flag.Value);
                }
            }
            args.Add(output.FilePath);
        }

        return new FfmpegCommand(ffmpegPath, args.ToArray(), workingDirectory);
    }
}
