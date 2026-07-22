// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Globalization;

namespace NoMercy.Encoder.Commands;

public class FfmpegCommandBuilder
{
    private GlobalOptions _globalOptions = new();
    private Dictionary<string, string>? _globalExtraFlags;
    private readonly List<InputOptions> _inputs = [];
    private string? _filterComplex;
    private readonly List<OutputOptions> _outputs = [];

    public FfmpegCommandBuilder WithGlobalOptions(GlobalOptions options)
    {
        _globalOptions = options;
        return this;
    }

    // Profile-level CustomArguments injected as global ffmpeg options (before the
    // -i input). Null is a no-op so non-customized profiles emit unchanged commands.
    public FfmpegCommandBuilder WithGlobalExtraFlags(Dictionary<string, string>? flags)
    {
        _globalExtraFlags = flags;
        return this;
    }

    public FfmpegCommandBuilder AddInput(InputOptions input)
    {
        _inputs.Add(item: input);
        return this;
    }

    public FfmpegCommandBuilder WithFilterComplex(string filterGraph)
    {
        _filterComplex = filterGraph;
        return this;
    }

    public FfmpegCommandBuilder AddOutput(OutputOptions output)
    {
        _outputs.Add(item: output);
        return this;
    }

    /// <summary>
    /// Whether anything has been added for this command to write. ffmpeg refuses
    /// to run without one ("At least one output file must be specified"), so a
    /// caller that may end up with nothing to encode must check before building.
    /// </summary>
    public bool HasOutputs => _outputs.Count > 0;

    public FfmpegCommand Build(string ffmpegPath, string? workingDirectory = null)
    {
        List<string> args = [];

        // Global options
        if (_globalOptions.Overwrite)
            args.Add(item: "-y");
        if (_globalOptions.HideBanner)
            args.Add(item: "-hide_banner");
        if (_globalOptions.ProgressPipe)
        {
            args.Add(item: "-progress");
            args.Add(item: "pipe:1");
        }
        if (_globalOptions.Threads.HasValue)
        {
            args.Add(item: "-threads");
            args.Add(item: _globalOptions.Threads.Value.ToString());
        }
        if (_globalOptions.ProbeSizeBytes.HasValue)
        {
            args.Add(item: "-probesize");
            args.Add(item: _globalOptions.ProbeSizeBytes.Value.ToString());
        }
        if (_globalOptions.AnalyzeDurationUs.HasValue)
        {
            args.Add(item: "-analyzeduration");
            args.Add(item: _globalOptions.AnalyzeDurationUs.Value.ToString());
        }

        // Profile-level custom args — global escape hatch, emitted before inputs.
        if (_globalExtraFlags is not null)
        {
            foreach (KeyValuePair<string, string> flag in _globalExtraFlags)
            {
                args.Add(item: flag.Key);
                // An empty value marks a bare boolean flag (e.g. "-an") — emitting
                // it anyway adds a stray empty argv token ffmpeg treats as an
                // unmapped output URL.
                if (flag.Value.Length > 0)
                    args.Add(item: flag.Value);
            }
        }

        // Inputs
        foreach (InputOptions input in _inputs)
        {
            if (input.HwAccelDevice is not null)
            {
                args.Add(item: "-hwaccel");
                args.Add(item: input.HwAccelDevice);
            }
            if (input.HwAccelOutputFormat is not null)
            {
                args.Add(item: "-hwaccel_output_format");
                args.Add(item: input.HwAccelOutputFormat);
            }
            if (input.SeekTo.HasValue)
            {
                args.Add(item: "-ss");
                args.Add(
                    item: input.SeekTo.Value.TotalSeconds.ToString(format: "F3", provider: CultureInfo.InvariantCulture)
                );
            }
            if (input.Duration.HasValue)
            {
                args.Add(item: "-t");
                args.Add(
                    item: input.Duration.Value.TotalSeconds.ToString(format: "F3", provider: CultureInfo.InvariantCulture)
                );
            }
            args.Add(item: "-i");
            args.Add(item: input.FilePath);
        }

        // Filter complex
        if (_filterComplex is not null)
        {
            args.Add(item: "-filter_complex");
            args.Add(item: _filterComplex);
        }

        // Outputs
        foreach (OutputOptions output in _outputs)
        {
            foreach (string map in output.MapStreams ?? [])
            {
                args.Add(item: "-map");
                args.Add(item: map);
            }
            if (output.VideoCodec is not null)
            {
                args.Add(item: "-c:v");
                args.Add(item: output.VideoCodec);
            }
            if (output.AudioCodec is not null)
            {
                args.Add(item: "-c:a");
                args.Add(item: output.AudioCodec);
            }
            if (output.SubtitleCodec is not null)
            {
                args.Add(item: "-c:s");
                args.Add(item: output.SubtitleCodec);
            }
            if (output.Preset is not null)
            {
                args.Add(item: "-preset");
                args.Add(item: output.Preset);
            }
            if (output.Profile is not null)
            {
                args.Add(item: "-profile:v");
                args.Add(item: output.Profile);
            }
            if (output.Level is not null)
            {
                args.Add(item: "-level");
                args.Add(item: output.Level);
            }
            if (output.PixelFormat is not null)
            {
                args.Add(item: "-pix_fmt");
                args.Add(item: output.PixelFormat);
            }
            if (output.Crf.HasValue)
            {
                args.Add(item: "-crf");
                args.Add(item: output.Crf.Value.ToString());
            }
            if (output.VideoBitrateKbps.HasValue)
            {
                args.Add(item: "-b:v");
                args.Add(item: $"{output.VideoBitrateKbps.Value}k");
            }
            if (output.AudioBitrateKbps.HasValue)
            {
                args.Add(item: "-b:a");
                args.Add(item: $"{output.AudioBitrateKbps.Value}k");
            }
            if (output.AudioChannels is not null)
            {
                args.Add(item: "-ac");
                args.Add(item: output.AudioChannels);
            }
            if (output.AudioSampleRate.HasValue)
            {
                args.Add(item: "-ar");
                args.Add(item: output.AudioSampleRate.Value.ToString());
            }
            if (output.KeyframeInterval.HasValue)
            {
                args.Add(item: "-g");
                args.Add(item: output.KeyframeInterval.Value.ToString());
            }
            if (output.ExtraFlags is not null)
            {
                foreach (KeyValuePair<string, string> flag in output.ExtraFlags)
                {
                    args.Add(item: flag.Key);
                    // An empty value marks a bare boolean flag (e.g. "-an") — emitting
                    // it anyway adds a stray empty argv token ffmpeg treats as an
                    // unmapped output URL.
                    if (flag.Value.Length > 0)
                        args.Add(item: flag.Value);
                }
            }

            // Metadata must sit with the output it applies to, after its codec and
            // filter flags and before its filepath — ffmpeg binds an output option
            // to the URL that follows it.
            if (output.StripSourceMetadata)
            {
                args.Add(item: "-map_metadata");
                args.Add(item: "-1");
            }

            foreach (OutputStreamTag tag in output.StreamMetadata ?? [])
            {
                args.Add(item: $"-metadata:{tag.StreamSpecifier}");
                // Unlike ExtraFlags, an empty value is meaningful here: "key=" tells
                // ffmpeg to drop the tag the source stream carried.
                args.Add(item: $"{tag.Key}={tag.Value}");
            }

            args.Add(item: output.FilePath);
        }

        return new(Executable: ffmpegPath, Arguments: args.ToArray(), WorkingDirectory: workingDirectory);
    }
}
