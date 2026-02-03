using System.Text;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Encoder.Dto;
using NoMercy.EncoderV2.Specifications.HLS;
using NoMercy.EncoderV2.Streams;
using NoMercy.NmSystem.Capabilities;

namespace NoMercy.EncoderV2.FFmpeg;

/// <summary>
/// Builds FFmpeg command strings from encoding profiles
/// Clean, testable implementation with DI support
/// </summary>
public class FFmpegCommandBuilder
{
    private readonly StreamAnalysis _analysis;
    private readonly EncoderProfile _profile;
    private readonly List<GpuAccelerator> _accelerators;
    private readonly string _inputFile;
    private readonly string _outputPath;
    private readonly ICodecSelector _codecSelector;
    private readonly HLSOutputMode _hlsOutputMode;
    private HLSOutputStructure? _hlsOutputStructure;
    private TimeSpan? _seekStart;
    private TimeSpan? _duration;

    public FFmpegCommandBuilder(
        StreamAnalysis analysis,
        EncoderProfile profile,
        List<GpuAccelerator> accelerators,
        string inputFile,
        string outputPath,
        ICodecSelector codecSelector,
        HLSOutputMode hlsOutputMode = HLSOutputMode.Combined)
    {
        _analysis = analysis;
        _profile = profile;
        _accelerators = accelerators;
        _inputFile = inputFile;
        _outputPath = outputPath;
        _codecSelector = codecSelector;
        _hlsOutputMode = hlsOutputMode;
    }

    /// <summary>
    /// Sets input seek position and duration for time-limited encoding
    /// Useful for quick tests or preview generation
    /// </summary>
    public void SetInputTimeRange(TimeSpan? seekStart, TimeSpan? duration)
    {
        _seekStart = seekStart;
        _duration = duration;
    }

    /// <summary>
    /// Sets the HLS output structure for separate streams mode
    /// </summary>
    public void SetHLSOutputStructure(HLSOutputStructure structure)
    {
        _hlsOutputStructure = structure;
    }

    public string BuildCommand()
    {
        StringBuilder command = new();

        string container = _profile.Container?.ToLower() ?? "mp4";
        bool isHls = container == "hls" || container == "m3u8";

        if (isHls && _hlsOutputMode == HLSOutputMode.SeparateStreams && _hlsOutputStructure != null)
        {
            return BuildSeparateStreamsCommand();
        }

        AppendGlobalOptions(command);
        AppendInputOptions(command);
        AppendVideoOptions(command);
        AppendAudioOptions(command);
        AppendSubtitleOptions(command);
        AppendOutputPath(command);

        return command.ToString().Trim();
    }

    /// <summary>
    /// Builds V1-compatible command with separate video/audio HLS streams using filter_complex
    /// </summary>
    private string BuildSeparateStreamsCommand()
    {
        if (_hlsOutputStructure == null)
        {
            throw new InvalidOperationException("HLS output structure must be set for separate streams mode");
        }

        StringBuilder command = new();

        AppendGlobalOptions(command);
        AppendInputOptions(command);

        // Build filter_complex for video and audio processing
        string filterComplex = BuildFilterComplex();
        if (!string.IsNullOrEmpty(filterComplex))
        {
            command.Append($"-filter_complex \"{filterComplex}\" ");
        }

        // Map and encode each video output
        int videoOutputIndex = 0;
        foreach (HLSVideoOutput videoOutput in _hlsOutputStructure.VideoOutputs)
        {
            IVideoProfile? videoProfile = _profile.VideoProfiles?.FirstOrDefault();
            if (videoProfile == null) continue;

            // Resolve codec with hardware acceleration
            string codec = _codecSelector.ResolveCodec(videoProfile.Codec);

            command.Append($"-map \"[v{videoOutputIndex}_hls]\" ");
            command.Append($"-c:v:{videoOutputIndex} {codec} ");

            if (videoProfile.Bitrate > 0)
            {
                command.Append($"-b:v:{videoOutputIndex} {videoProfile.Bitrate}k ");
            }

            if (videoProfile.Crf > 0)
            {
                command.Append($"-crf {videoProfile.Crf} ");
            }

            if (!string.IsNullOrEmpty(videoProfile.Preset))
            {
                command.Append($"-preset {videoProfile.Preset} ");
            }

            if (!string.IsNullOrEmpty(videoProfile.Profile))
            {
                command.Append($"-profile:v:{videoOutputIndex} {videoProfile.Profile} ");
            }

            if (videoProfile.KeyInt > 0)
            {
                command.Append($"-g {videoProfile.KeyInt} ");
            }

            // HLS options for this video stream
            command.Append($"-f hls ");
            command.Append($"-hls_time 6 ");
            command.Append($"-hls_playlist_type vod ");
            command.Append($"-hls_segment_filename \"{Path.Combine(videoOutput.FolderPath, videoOutput.SegmentPattern)}\" ");
            command.Append($"\"{videoOutput.PlaylistPath}\" ");

            videoOutputIndex++;
        }

        // Map and encode each audio output
        int audioOutputIndex = 0;
        foreach (HLSAudioOutput audioOutput in _hlsOutputStructure.AudioOutputs)
        {
            IAudioProfile? audioProfile = _profile.AudioProfiles?.FirstOrDefault(a =>
                a.Codec.Contains(audioOutput.Codec, StringComparison.OrdinalIgnoreCase));

            if (audioProfile == null)
            {
                audioProfile = _profile.AudioProfiles?.FirstOrDefault();
            }

            if (audioProfile == null) continue;

            command.Append($"-map \"[a{audioOutputIndex}_{audioOutput.Codec}]\" ");
            command.Append($"-c:a:{audioOutputIndex} {audioProfile.Codec} ");

            if (audioProfile.Channels > 0)
            {
                command.Append($"-ac {audioProfile.Channels} ");
            }

            if (audioProfile.SampleRate > 0)
            {
                command.Append($"-ar {audioProfile.SampleRate} ");
            }

            if (audioProfile.Opts != null && audioProfile.Opts.Length > 0)
            {
                foreach (string opt in audioProfile.Opts)
                {
                    command.Append($"{opt} ");
                }
            }

            // HLS options for this audio stream
            command.Append($"-f hls ");
            command.Append($"-hls_time 6 ");
            command.Append($"-hls_playlist_type vod ");
            command.Append($"-hls_segment_filename \"{Path.Combine(audioOutput.FolderPath, audioOutput.SegmentPattern)}\" ");
            command.Append($"\"{audioOutput.PlaylistPath}\" ");

            audioOutputIndex++;
        }

        return command.ToString().Trim();
    }

    /// <summary>
    /// Builds filter_complex for separate video/audio processing
    /// </summary>
    private string BuildFilterComplex()
    {
        if (_hlsOutputStructure == null) return string.Empty;

        List<string> filters = [];

        // Build video filters
        int videoIndex = 0;
        foreach (HLSVideoOutput videoOutput in _hlsOutputStructure.VideoOutputs)
        {
            IVideoProfile? videoProfile = _profile.VideoProfiles?.FirstOrDefault();
            if (videoProfile == null) continue;

            List<string> videoFilters = [];

            // Scale filter
            if (videoProfile.Width > 0 && videoProfile.Height > 0)
            {
                videoFilters.Add($"scale={videoProfile.Width}:{videoProfile.Height}");
            }

            // HDR to SDR tonemapping if needed
            if (videoProfile.ConvertHdrToSdr && _analysis.IsHDR)
            {
                string tonemapChain = "zscale=tin=smpte2084:min=bt2020nc:pin=bt2020:rin=tv:t=smpte2084:m=bt2020nc:p=bt2020:r=tv," +
                                     "zscale=t=linear:npl=100," +
                                     "format=gbrpf32le," +
                                     "zscale=p=bt709," +
                                     "tonemap=tonemap=hable:desat=0," +
                                     "zscale=t=bt709:m=bt709:r=tv," +
                                     "format=yuv420p";
                videoFilters.Add(tonemapChain);
            }
            else
            {
                // Ensure yuv420p for non-HDR or when not converting
                videoFilters.Add("format=yuv420p");
            }

            string videoFilterChain = string.Join(",", videoFilters);
            filters.Add($"[0:v:{videoIndex}]{videoFilterChain}[v{videoIndex}_hls]");

            videoIndex++;
        }

        // Build audio filters (copy audio streams to labeled outputs)
        int audioIndex = 0;
        foreach (HLSAudioOutput audioOutput in _hlsOutputStructure.AudioOutputs)
        {
            // Simple copy for audio (actual encoding happens in output mapping)
            filters.Add($"[0:a:{audioIndex}]acopy[a{audioIndex}_{audioOutput.Codec}]");
            audioIndex++;
        }

        return string.Join(";", filters);
    }

    private void AppendGlobalOptions(StringBuilder command)
    {
        command.Append("-hide_banner ");
        command.Append("-probesize 4092M -analyzeduration 9999M ");

        int threads = Environment.ProcessorCount;
        command.Append($"-threads {threads} ");

        foreach (GpuAccelerator accelerator in _accelerators)
        {
            command.Append($"{accelerator.FfmpegArgs} ");
        }

        command.Append("-progress - ");
    }

    private void AppendInputOptions(StringBuilder command)
    {
        command.Append("-y ");

        // Add seek and duration options before input (faster seeking)
        if (_seekStart.HasValue)
        {
            command.Append($"-ss {_seekStart.Value:hh\\:mm\\:ss} ");
        }

        if (_duration.HasValue)
        {
            command.Append($"-t {_duration.Value:hh\\:mm\\:ss} ");
        }

        command.Append($"-i \"{_inputFile}\" ");
        command.Append("-map_metadata -1 ");
    }

    private void AppendVideoOptions(StringBuilder command)
    {
        if (_profile.VideoProfiles == null || _profile.VideoProfiles.Length == 0)
        {
            return;
        }

        foreach (IVideoProfile videoProfile in _profile.VideoProfiles)
        {
            command.Append($"-map 0:v:0 ");
            command.Append($"-c:v {videoProfile.Codec} ");

            if (videoProfile.Bitrate > 0)
            {
                command.Append($"-b:v {videoProfile.Bitrate}k ");
            }

            if (videoProfile.Crf > 0)
            {
                command.Append($"-crf {videoProfile.Crf} ");
            }

            if (!string.IsNullOrEmpty(videoProfile.Preset))
            {
                command.Append($"-preset {videoProfile.Preset} ");
            }

            if (!string.IsNullOrEmpty(videoProfile.Profile))
            {
                command.Append($"-profile:v {videoProfile.Profile} ");
            }

            if (!string.IsNullOrEmpty(videoProfile.Tune))
            {
                command.Append($"-tune {videoProfile.Tune} ");
            }

            if (videoProfile.Width > 0 && videoProfile.Height > 0)
            {
                command.Append($"-vf \"scale={videoProfile.Width}:{videoProfile.Height}\" ");
            }

            if (videoProfile.Framerate > 0)
            {
                command.Append($"-r {videoProfile.Framerate} ");
            }

            if (videoProfile.KeyInt > 0)
            {
                command.Append($"-g {videoProfile.KeyInt} ");
            }

            if (videoProfile.ConvertHdrToSdr && _analysis.IsHDR)
            {
                command.Append("-vf \"zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,tonemap=tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv,format=yuv420p\" ");
            }

            if (videoProfile.Opts != null && videoProfile.Opts.Length > 0)
            {
                foreach (string opt in videoProfile.Opts)
                {
                    command.Append($"{opt} ");
                }
            }
        }
    }

    private void AppendAudioOptions(StringBuilder command)
    {
        if (_profile.AudioProfiles == null || _profile.AudioProfiles.Length == 0)
        {
            return;
        }

        foreach (IAudioProfile audioProfile in _profile.AudioProfiles)
        {
            command.Append($"-map 0:a:0 ");
            command.Append($"-c:a {audioProfile.Codec} ");

            if (audioProfile.Channels > 0)
            {
                command.Append($"-ac {audioProfile.Channels} ");
            }

            if (audioProfile.SampleRate > 0)
            {
                command.Append($"-ar {audioProfile.SampleRate} ");
            }

            if (audioProfile.Opts != null && audioProfile.Opts.Length > 0)
            {
                foreach (string opt in audioProfile.Opts)
                {
                    command.Append($"{opt} ");
                }
            }
        }
    }

    private void AppendSubtitleOptions(StringBuilder command)
    {
        if (_profile.SubtitleProfiles == null || _profile.SubtitleProfiles.Length == 0)
        {
            return;
        }

        if (!_analysis.HasSubtitles)
        {
            return;
        }

        foreach (ISubtitleProfile subtitleProfile in _profile.SubtitleProfiles)
        {
            command.Append($"-map 0:s? ");
            command.Append($"-c:s {subtitleProfile.Codec} ");

            if (subtitleProfile.Opts != null && subtitleProfile.Opts.Length > 0)
            {
                foreach (string opt in subtitleProfile.Opts)
                {
                    command.Append($"{opt} ");
                }
            }
        }
    }

    private void AppendOutputPath(StringBuilder command)
    {
        string container = _profile.Container?.ToLower() ?? "mp4";

        if (container == "hls" || container == "m3u8")
        {
            command.Append("-f hls ");
            command.Append("-hls_time 4 ");
            command.Append("-hls_playlist_type vod ");
            command.Append($"-hls_segment_filename \"{Path.Combine(_outputPath, "segment_%05d.ts")}\" ");
            command.Append($"\"{Path.Combine(_outputPath, "playlist.m3u8")}\"");
        }
        else if (container == "mp4")
        {
            command.Append("-movflags +faststart ");
            command.Append($"\"{Path.Combine(_outputPath, "output.mp4")}\"");
        }
        else
        {
            command.Append($"\"{Path.Combine(_outputPath, $"output.{container}")}\"");
        }
    }
}
