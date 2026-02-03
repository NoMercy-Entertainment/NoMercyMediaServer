using System.Text;
using NoMercy.EncoderV2.Abstractions;
using NoMercy.EncoderV2.Containers;

namespace NoMercy.EncoderV2.Commands;

/// <summary>
/// Builds FFmpeg command arguments from encoding profiles
/// </summary>
public sealed class FFmpegCommandBuilder
{
    private readonly List<string> _globalOptions = [];
    private readonly List<string> _inputOptions = [];
    private readonly List<string> _outputOptions = [];
    private readonly List<string> _filters = [];
    private readonly List<string> _filterComplex = [];
    private string? _inputPath;
    private string? _outputPath;

    /// <summary>
    /// Creates a new command builder
    /// </summary>
    public static FFmpegCommandBuilder Create() => new();

    /// <summary>
    /// Adds global options (before input)
    /// </summary>
    public FFmpegCommandBuilder AddGlobalOptions(params string[] options)
    {
        _globalOptions.AddRange(options);
        return this;
    }

    /// <summary>
    /// Sets the input file
    /// </summary>
    public FFmpegCommandBuilder WithInput(string inputPath)
    {
        _inputPath = inputPath;
        return this;
    }

    /// <summary>
    /// Adds input options (before -i)
    /// </summary>
    public FFmpegCommandBuilder AddInputOptions(params string[] options)
    {
        _inputOptions.AddRange(options);
        return this;
    }

    /// <summary>
    /// Sets the output file
    /// </summary>
    public FFmpegCommandBuilder WithOutput(string outputPath)
    {
        _outputPath = outputPath;
        return this;
    }

    /// <summary>
    /// Adds output options
    /// </summary>
    public FFmpegCommandBuilder AddOutputOptions(params string[] options)
    {
        _outputOptions.AddRange(options);
        return this;
    }

    /// <summary>
    /// Adds a video filter (-vf)
    /// </summary>
    public FFmpegCommandBuilder AddVideoFilter(string filter)
    {
        _filters.Add(filter);
        return this;
    }

    /// <summary>
    /// Adds a complex filter (-filter_complex)
    /// </summary>
    public FFmpegCommandBuilder AddComplexFilter(string filter)
    {
        _filterComplex.Add(filter);
        return this;
    }

    /// <summary>
    /// Configures hardware acceleration
    /// </summary>
    public FFmpegCommandBuilder WithHardwareAcceleration(HardwareAcceleration acceleration)
    {
        IReadOnlyList<string> args = Services.HardwareAccelerationDetector.GetHwaccelInputArgs(acceleration);
        _inputOptions.AddRange(args);
        return this;
    }

    /// <summary>
    /// Adds thread configuration
    /// </summary>
    public FFmpegCommandBuilder WithThreads(int threads)
    {
        if (threads > 0)
        {
            _globalOptions.AddRange(["-threads", threads.ToString()]);
        }
        return this;
    }

    /// <summary>
    /// Enables overwrite output
    /// </summary>
    public FFmpegCommandBuilder WithOverwrite(bool overwrite = true)
    {
        if (overwrite)
        {
            _globalOptions.Add("-y");
        }
        return this;
    }

    /// <summary>
    /// Builds the final command string
    /// </summary>
    public string Build()
    {
        if (string.IsNullOrEmpty(_inputPath))
        {
            throw new InvalidOperationException("Input path is required");
        }

        if (string.IsNullOrEmpty(_outputPath))
        {
            throw new InvalidOperationException("Output path is required");
        }

        StringBuilder sb = new();

        // Global options
        if (_globalOptions.Count > 0)
        {
            sb.Append(string.Join(" ", _globalOptions));
            sb.Append(' ');
        }

        // Input options
        if (_inputOptions.Count > 0)
        {
            sb.Append(string.Join(" ", _inputOptions));
            sb.Append(' ');
        }

        // Input file
        sb.Append($"-i \"{_inputPath}\" ");

        // Filters
        if (_filterComplex.Count > 0)
        {
            sb.Append($"-filter_complex \"{string.Join(";", _filterComplex)}\" ");
        }
        else if (_filters.Count > 0)
        {
            sb.Append($"-vf \"{string.Join(",", _filters)}\" ");
        }

        // Output options
        if (_outputOptions.Count > 0)
        {
            sb.Append(string.Join(" ", _outputOptions));
            sb.Append(' ');
        }

        // Output file
        sb.Append($"\"{_outputPath}\"");

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Builds an FFmpeg command from a complete encoding profile
    /// </summary>
    public static FFmpegCommandBuilder FromProfile(
        string inputPath,
        string outputPath,
        IEncodingProfile profile,
        MediaInfo? sourceInfo = null)
    {
        FFmpegCommandBuilder builder = Create()
            .WithInput(inputPath)
            .WithOutput(outputPath)
            .WithOverwrite(profile.Options.OverwriteOutput);

        // Configure threads
        if (profile.Options.Threads > 0)
        {
            builder.WithThreads(profile.Options.Threads);
        }

        // Hardware acceleration
        if (profile.Options.UseHardwareAcceleration && profile.Options.PreferredHardwareAcceleration.HasValue)
        {
            builder.WithHardwareAcceleration(profile.Options.PreferredHardwareAcceleration.Value);
        }

        // Add additional global arguments
        if (profile.Options.AdditionalArguments.Count > 0)
        {
            builder.AddGlobalOptions([.. profile.Options.AdditionalArguments]);
        }

        // Build video codec arguments
        foreach (VideoOutputConfig videoOutput in profile.VideoOutputs)
        {
            // Skip if source is lower resolution
            if (videoOutput.SkipIfLowerResolution && sourceInfo != null)
            {
                VideoStreamInfo? primaryVideo = sourceInfo.VideoStreams.FirstOrDefault();
                if (primaryVideo != null)
                {
                    if (videoOutput.Width.HasValue && primaryVideo.Width < videoOutput.Width.Value)
                    {
                        continue;
                    }
                    if (videoOutput.Height.HasValue && primaryVideo.Height < videoOutput.Height.Value)
                    {
                        continue;
                    }
                }
            }

            // Add codec arguments
            builder.AddOutputOptions([.. videoOutput.Codec.BuildArguments()]);

            // Add scale filter if dimensions specified
            if (videoOutput.Width.HasValue || videoOutput.Height.HasValue)
            {
                string scaleFilter = BuildScaleFilter(videoOutput, sourceInfo);
                builder.AddVideoFilter(scaleFilter);
            }

            // Add custom filters
            foreach (string filter in videoOutput.Filters)
            {
                builder.AddVideoFilter(filter);
            }

            // Add tone mapping if needed
            if (videoOutput.ToneMap && sourceInfo?.VideoStreams.FirstOrDefault()?.IsHdr == true)
            {
                builder.AddVideoFilter(BuildToneMappingFilter());
            }
        }

        // Build audio codec arguments
        foreach (AudioOutputConfig audioOutput in profile.AudioOutputs)
        {
            builder.AddOutputOptions([.. audioOutput.Codec.BuildArguments()]);

            foreach (string filter in audioOutput.Filters)
            {
                builder.AddOutputOptions(["-af", filter]);
            }
        }

        // Build subtitle codec arguments
        foreach (SubtitleOutputConfig subtitleOutput in profile.SubtitleOutputs)
        {
            builder.AddOutputOptions([.. subtitleOutput.Codec.BuildArguments()]);
        }

        // Build container arguments
        builder.AddOutputOptions([.. profile.Container.BuildArguments()]);

        // Add HLS-specific options
        if (profile.Container is IHlsContainer hlsContainer)
        {
            // Add bitstream filter for H.264/H.265
            VideoOutputConfig? videoConfig = profile.VideoOutputs.FirstOrDefault();
            if (videoConfig != null)
            {
                string bsf = HlsContainer.GetBitstreamFilter(videoConfig.Codec.Name);
                if (!string.IsNullOrEmpty(bsf))
                {
                    builder.AddOutputOptions(["-bsf:v", bsf]);
                }
            }

            // Set keyframe interval to match segment duration
            int keyframeInterval = (int)(hlsContainer.SegmentDuration * 30); // Assume 30fps default
            builder.AddOutputOptions(["-force_key_frames", $"expr:gte(t,n_forced*{hlsContainer.SegmentDuration})"]);
        }

        return builder;
    }

    private static string BuildScaleFilter(VideoOutputConfig config, MediaInfo? sourceInfo)
    {
        int width = config.Width ?? -1;
        int height = config.Height ?? -1;

        // If only one dimension specified, preserve aspect ratio
        if (width > 0 && height <= 0)
        {
            height = -2; // -2 for divisible by 2
        }
        else if (height > 0 && width <= 0)
        {
            width = -2;
        }

        // Build scale filter based on mode
        string scaleFilter = config.ScaleMode switch
        {
            ScaleMode.Fit => $"scale={width}:{height}:force_original_aspect_ratio=decrease",
            ScaleMode.Fill => $"scale={width}:{height}:force_original_aspect_ratio=increase,crop={width}:{height}",
            ScaleMode.Stretch => $"scale={width}:{height}",
            ScaleMode.DownscaleOnly => $"scale='min({width},iw)':min'({height},ih)':force_original_aspect_ratio=decrease",
            _ => $"scale={width}:{height}"
        };

        // Ensure even dimensions
        return scaleFilter + ",pad=ceil(iw/2)*2:ceil(ih/2)*2";
    }

    private static string BuildToneMappingFilter()
    {
        // HDR to SDR tone mapping using zscale
        return "zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv,format=yuv420p";
    }
}

/// <summary>
/// Helper for building complex filter graphs
/// </summary>
public sealed class FilterGraphBuilder
{
    private readonly List<FilterNode> _nodes = [];
    private int _labelCounter;

    /// <summary>
    /// Adds an input node
    /// </summary>
    public FilterGraphBuilder AddInput(int streamIndex, out string label)
    {
        label = $"[{streamIndex}:v]";
        return this;
    }

    /// <summary>
    /// Adds a filter node
    /// </summary>
    public FilterGraphBuilder AddFilter(string inputLabel, string filter, out string outputLabel)
    {
        outputLabel = $"[v{_labelCounter++}]";
        _nodes.Add(new FilterNode
        {
            Input = inputLabel,
            Filter = filter,
            Output = outputLabel
        });
        return this;
    }

    /// <summary>
    /// Adds a split filter for multiple outputs
    /// </summary>
    public FilterGraphBuilder AddSplit(string inputLabel, int outputs, out string[] outputLabels)
    {
        outputLabels = new string[outputs];
        StringBuilder splitFilter = new();
        splitFilter.Append($"{inputLabel}split={outputs}");

        for (int i = 0; i < outputs; i++)
        {
            outputLabels[i] = $"[s{_labelCounter++}]";
            splitFilter.Append(outputLabels[i]);
        }

        _nodes.Add(new FilterNode
        {
            Input = "",
            Filter = splitFilter.ToString(),
            Output = ""
        });

        return this;
    }

    /// <summary>
    /// Builds the filter graph string
    /// </summary>
    public string Build()
    {
        return string.Join(";", _nodes.Select(n =>
            string.IsNullOrEmpty(n.Input)
                ? n.Filter
                : $"{n.Input}{n.Filter}{n.Output}"));
    }

    private sealed class FilterNode
    {
        public string Input { get; init; } = "";
        public string Filter { get; init; } = "";
        public string Output { get; init; } = "";
    }
}
