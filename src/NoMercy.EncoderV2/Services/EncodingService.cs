using NoMercy.Database.Models;
using NoMercy.EncoderV2.FFmpeg;
using NoMercy.EncoderV2.Hardware;
using NoMercy.EncoderV2.Specifications.HLS;
using NoMercy.EncoderV2.Streams;
using NoMercy.NmSystem.Capabilities;

namespace NoMercy.EncoderV2.Services;

/// <summary>
/// High-level encoding service that orchestrates the complete encoding process
/// This is the main entry point for encoding operations in EncoderV2
/// </summary>
public interface IEncodingService
{
    /// <summary>
    /// Encodes a media file using the specified profile
    /// Handles analysis, command building, execution, and playlist generation
    /// </summary>
    Task<EncodingResult> EncodeAsync(
        string inputFile,
        string outputFolder,
        EncoderProfile profile,
        Action<string>? progressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Encodes with V1-compatible separate stream output
    /// </summary>
    Task<EncodingResult> EncodeWithSeparateStreamsAsync(
        string inputFile,
        string outputFolder,
        string baseFilename,
        EncoderProfile profile,
        Action<string>? progressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Quick preview encoding with time limits
    /// </summary>
    Task<EncodingResult> EncodePreviewAsync(
        string inputFile,
        string outputFolder,
        EncoderProfile profile,
        TimeSpan seekStart,
        TimeSpan duration,
        Action<string>? progressCallback = null,
        CancellationToken cancellationToken = default);
}

public class EncodingService : IEncodingService
{
    private readonly IStreamAnalyzer _streamAnalyzer;
    private readonly IHardwareAccelerationService _hardwareService;
    private readonly ICodecSelector _codecSelector;
    private readonly IFFmpegService _ffmpegService;
    private readonly IHLSOutputOrchestrator _hlsOrchestrator;

    public EncodingService(
        IStreamAnalyzer streamAnalyzer,
        IHardwareAccelerationService hardwareService,
        ICodecSelector codecSelector,
        IFFmpegService ffmpegService,
        IHLSOutputOrchestrator hlsOrchestrator)
    {
        _streamAnalyzer = streamAnalyzer;
        _hardwareService = hardwareService;
        _codecSelector = codecSelector;
        _ffmpegService = ffmpegService;
        _hlsOrchestrator = hlsOrchestrator;
    }

    public async Task<EncodingResult> EncodeAsync(
        string inputFile,
        string outputFolder,
        EncoderProfile profile,
        Action<string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        // Validate inputs
        if (!File.Exists(inputFile))
        {
            return EncodingResult.Failure($"Input file not found: {inputFile}");
        }

        Directory.CreateDirectory(outputFolder);

        // Analyze media
        StreamAnalysis analysis = await _streamAnalyzer.AnalyzeAsync(inputFile, cancellationToken);

        // Get hardware accelerators
        List<GpuAccelerator> accelerators = _hardwareService.GetAvailableAccelerators();

        // Build FFmpeg command
        FFmpegCommandBuilder commandBuilder = new(
            analysis,
            profile,
            accelerators,
            inputFile,
            outputFolder,
            _codecSelector,
            HLSOutputMode.Combined);

        string command = commandBuilder.BuildCommand();

        // Execute encoding
        FFmpegExecutionResult executionResult = await _ffmpegService.ExecuteAsync(
            command,
            outputFolder,
            progressCallback,
            cancellationToken);

        return new EncodingResult
        {
            Success = executionResult.Success,
            OutputPath = outputFolder,
            Duration = executionResult.ExecutionTime,
            ErrorMessage = executionResult.ErrorMessage,
            ExitCode = executionResult.ExitCode
        };
    }

    public async Task<EncodingResult> EncodeWithSeparateStreamsAsync(
        string inputFile,
        string outputFolder,
        string baseFilename,
        EncoderProfile profile,
        Action<string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        // Validate inputs
        if (!File.Exists(inputFile))
        {
            return EncodingResult.Failure($"Input file not found: {inputFile}");
        }

        Directory.CreateDirectory(outputFolder);

        // Analyze media
        StreamAnalysis analysis = await _streamAnalyzer.AnalyzeAsync(inputFile, cancellationToken);

        // Get hardware accelerators
        List<GpuAccelerator> accelerators = _hardwareService.GetAvailableAccelerators();

        // Create HLS output structure
        HLSSpecification hlsSpec = new()
        {
            Version = 3,
            TargetDuration = 10,
            SegmentDuration = 6,
            PlaylistType = "VOD",
            IndependentSegments = true
        };

        HLSOutputStructure outputStructure = await _hlsOrchestrator.CreateOutputStructureFromStreamAnalysisAsync(
            outputFolder,
            baseFilename,
            analysis,
            hlsSpec);

        DateTime startTime = DateTime.Now;
        bool allSuccessful = true;
        string lastError = string.Empty;
        int lastExitCode = 0;

        // Encode each video stream separately
        foreach (HLSVideoOutput videoOutput in outputStructure.VideoOutputs)
        {
            FFmpegExecutionResult result = await EncodeVideoStreamAsync(
                inputFile,
                videoOutput,
                analysis,
                profile,
                accelerators,
                progressCallback,
                cancellationToken);

            if (!result.Success && result.ExitCode != 0)
            {
                allSuccessful = false;
                lastError = $"Video stream encoding failed: {result.ErrorMessage}\nFFmpeg stderr: {result.StandardError}";
                lastExitCode = result.ExitCode;
            }
        }

        // Encode each audio stream separately
        foreach (HLSAudioOutput audioOutput in outputStructure.AudioOutputs)
        {
            FFmpegExecutionResult result = await EncodeAudioStreamAsync(
                inputFile,
                audioOutput,
                analysis,
                profile,
                accelerators,
                progressCallback,
                cancellationToken);

            if (!result.Success && result.ExitCode != 0)
            {
                allSuccessful = false;
                lastError = $"Audio stream encoding failed: {result.ErrorMessage}\nFFmpeg stderr: {result.StandardError}";
                lastExitCode = result.ExitCode;
            }
        }

        // Generate playlists
        if (allSuccessful)
        {
            await _hlsOrchestrator.GeneratePlaylistsAsync(outputStructure, analysis.Duration);
        }

        return new EncodingResult
        {
            Success = allSuccessful,
            OutputPath = outputFolder,
            Duration = DateTime.Now - startTime,
            ErrorMessage = allSuccessful ? null : lastError,
            ExitCode = lastExitCode,
            HLSOutputStructure = outputStructure
        };
    }

    public async Task<EncodingResult> EncodePreviewAsync(
        string inputFile,
        string outputFolder,
        EncoderProfile profile,
        TimeSpan seekStart,
        TimeSpan duration,
        Action<string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        // Validate inputs
        if (!File.Exists(inputFile))
        {
            return EncodingResult.Failure($"Input file not found: {inputFile}");
        }

        Directory.CreateDirectory(outputFolder);

        // Analyze media
        StreamAnalysis analysis = await _streamAnalyzer.AnalyzeAsync(inputFile, cancellationToken);

        // Get hardware accelerators
        List<GpuAccelerator> accelerators = _hardwareService.GetAvailableAccelerators();

        // Build FFmpeg command with time range
        FFmpegCommandBuilder commandBuilder = new(
            analysis,
            profile,
            accelerators,
            inputFile,
            outputFolder,
            _codecSelector,
            HLSOutputMode.Combined);

        commandBuilder.SetInputTimeRange(seekStart, duration);
        string command = commandBuilder.BuildCommand();

        // Execute encoding
        FFmpegExecutionResult executionResult = await _ffmpegService.ExecuteAsync(
            command,
            outputFolder,
            progressCallback,
            cancellationToken);

        return new EncodingResult
        {
            Success = executionResult.Success,
            OutputPath = outputFolder,
            Duration = executionResult.ExecutionTime,
            ErrorMessage = executionResult.ErrorMessage,
            ExitCode = executionResult.ExitCode
        };
    }

    private async Task<FFmpegExecutionResult> EncodeVideoStreamAsync(
        string inputFile,
        HLSVideoOutput videoOutput,
        StreamAnalysis analysis,
        EncoderProfile profile,
        List<GpuAccelerator> accelerators,
        Action<string>? progressCallback,
        CancellationToken cancellationToken)
    {
        Database.IVideoProfile? videoProfile = profile.VideoProfiles?.FirstOrDefault();
        if (videoProfile == null)
        {
            return FFmpegExecutionResult.Failure("No video profile found");
        }

        string hwAccelArgs = accelerators.Count > 0
            ? accelerators[0].FfmpegArgs
            : string.Empty;

        List<string> commandParts = [];

        commandParts.Add("-y");
        commandParts.Add("-hide_banner");

        if (!string.IsNullOrEmpty(hwAccelArgs))
        {
            commandParts.Add(hwAccelArgs.Trim());
        }

        commandParts.Add($"-i \"{inputFile}\"");
        commandParts.Add($"-map 0:v:0");

        string selectedCodec = videoProfile.Codec.ToLower() switch
        {
            "h264" or "libx264" => _codecSelector.SelectH264Codec(),
            "h265" or "hevc" or "libx265" => _codecSelector.SelectH265Codec(),
            _ => videoProfile.Codec
        };
        commandParts.Add($"-c:v {selectedCodec}");

        if (videoProfile.Bitrate > 0)
        {
            commandParts.Add($"-b:v {videoProfile.Bitrate}k");
        }

        if (!string.IsNullOrEmpty(videoProfile.Preset))
        {
            commandParts.Add($"-preset {videoProfile.Preset}");
        }

        List<string> filters = [];
        if (videoProfile.Width > 0 && videoProfile.Height > 0)
        {
            filters.Add($"scale={videoProfile.Width}:{videoProfile.Height}");
        }

        if (videoProfile.ConvertHdrToSdr && analysis.IsHDR)
        {
            string tonemapChain = "zscale=tin=smpte2084:min=bt2020nc:pin=bt2020:rin=tv:t=smpte2084:m=bt2020nc:p=bt2020:r=tv," +
                                 "zscale=t=linear:npl=100," +
                                 "format=gbrpf32le," +
                                 "zscale=p=bt709," +
                                 "tonemap=tonemap=hable:desat=0," +
                                 "zscale=t=bt709:m=bt709:r=tv," +
                                 "format=yuv420p";
            filters.Add(tonemapChain);
        }

        if (filters.Count > 0)
        {
            commandParts.Add($"-vf \"{string.Join(",", filters)}\"");
        }

        commandParts.Add("-an");
        commandParts.Add("-f hls");
        commandParts.Add($"-hls_time 6");
        commandParts.Add($"-hls_playlist_type vod");
        commandParts.Add($"-hls_segment_filename \"{Path.Combine(videoOutput.FolderPath, videoOutput.SegmentPattern)}\"");
        commandParts.Add($"\"{videoOutput.PlaylistPath}\"");

        string command = string.Join(" ", commandParts);

        return await _ffmpegService.ExecuteAsync(
            command,
            videoOutput.FolderPath,
            progressCallback,
            cancellationToken);
    }

    private async Task<FFmpegExecutionResult> EncodeAudioStreamAsync(
        string inputFile,
        HLSAudioOutput audioOutput,
        StreamAnalysis analysis,
        EncoderProfile profile,
        List<GpuAccelerator> accelerators,
        Action<string>? progressCallback,
        CancellationToken cancellationToken)
    {
        Database.IAudioProfile? audioProfile = profile.AudioProfiles?.FirstOrDefault(a =>
            a.Codec.Contains(audioOutput.Codec, StringComparison.OrdinalIgnoreCase));

        if (audioProfile == null)
        {
            audioProfile = profile.AudioProfiles?.FirstOrDefault();
        }

        if (audioProfile == null)
        {
            return FFmpegExecutionResult.Failure("No audio profile found");
        }

        List<string> commandParts = [];

        commandParts.Add("-y");
        commandParts.Add("-hide_banner");
        commandParts.Add($"-i \"{inputFile}\"");
        commandParts.Add($"-map 0:a:0");
        commandParts.Add($"-c:a {audioProfile.Codec}");

        if (audioProfile.Channels > 0)
        {
            commandParts.Add($"-ac {audioProfile.Channels}");
        }

        if (audioProfile.SampleRate > 0)
        {
            commandParts.Add($"-ar {audioProfile.SampleRate}");
        }

        if (audioProfile.Opts != null && audioProfile.Opts.Length > 0)
        {
            foreach (string opt in audioProfile.Opts)
            {
                commandParts.Add(opt);
            }
        }

        commandParts.Add("-vn");
        commandParts.Add("-f hls");
        commandParts.Add($"-hls_time 6");
        commandParts.Add($"-hls_playlist_type vod");
        commandParts.Add($"-hls_segment_filename \"{Path.Combine(audioOutput.FolderPath, audioOutput.SegmentPattern)}\"");
        commandParts.Add($"\"{audioOutput.PlaylistPath}\"");

        string command = string.Join(" ", commandParts);

        return await _ffmpegService.ExecuteAsync(
            command,
            audioOutput.FolderPath,
            progressCallback,
            cancellationToken);
    }
}

/// <summary>
/// Result of an encoding operation
/// </summary>
public class EncodingResult
{
    public bool Success { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
    public int ExitCode { get; set; }
    public HLSOutputStructure? HLSOutputStructure { get; set; }

    public static EncodingResult Failure(string errorMessage)
    {
        return new EncodingResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            ExitCode = -1
        };
    }
}
