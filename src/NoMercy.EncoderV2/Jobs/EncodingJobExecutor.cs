using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using NoMercy.NmSystem.Information;

namespace NoMercy.EncoderV2.Jobs;

/// <summary>
/// Core encoding job execution logic - QUEUE INDEPENDENT
/// This class contains all FFmpeg execution, progress monitoring, and post-processing
/// Can be used by any queue system (Server's NoMercy.Queue, EncoderNode's custom queues, etc.)
/// 
/// Dependencies: None on Queue projects, only on core encoding infrastructure
/// </summary>
public class EncodingJobExecutor
{
    /// <summary>
    /// Execute encoding job completely
    /// Returns final status with completion state
    /// </summary>
    public async Task<EncodingJobStatus> ExecuteAsync(EncodingJobPayload payload, CancellationToken cancellationToken = default)
    {
        payload.Status.State = "encoding";
        payload.Status.StartedAt = DateTime.UtcNow;

        try
        {
            // Step 1: Validate input file exists
            if (!File.Exists(payload.Input.FilePath))
            {
                throw new FileNotFoundException($"Input file not found: {payload.Input.FilePath}");
            }

            // Step 2: Apply job rules to get post-processing actions
            List<PostProcessingAction> postProcessingActions = await GetPostProcessingActionsAsync(payload);

            // Step 3: Build FFmpeg command
            string ffmpegCommand = BuildFfmpegCommand(payload);
            payload.Status.ExecutionCommand = ffmpegCommand;

            // Step 4: Execute FFmpeg with progress monitoring
            await ExecuteFfmpegAsync(payload, ffmpegCommand, cancellationToken);

            // Step 5: Execute post-processing actions (font extraction, subtitle conversion, etc.)
            await ExecutePostProcessingActionsAsync(payload, postProcessingActions, cancellationToken);

            // Step 6: Validate output before completing
            await ValidateOutputAsync(payload);

            payload.Status.State = "completed";
            payload.Status.CompletedAt = DateTime.UtcNow;
        }
        catch (OperationCanceledException)
        {
            payload.Status.State = "cancelled";
            payload.Status.ErrorMessage = "Encoding was cancelled";
            payload.Status.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            payload.Status.State = "failed";
            payload.Status.ErrorMessage = ex.Message;
            payload.Status.CompletedAt = DateTime.UtcNow;
        }

        return payload.Status;
    }

    /// <summary>
    /// Build complete FFmpeg command from EncodingJobPayload
    /// Uses StringBuilder for efficiency and Dictionary for clear option mapping
    /// </summary>
    public string BuildFfmpegCommand(EncodingJobPayload payload)
    {
        Dictionary<string, string?> options = new();
        StringBuilder sb = new();

        // Input file
        sb.Append($"ffmpeg -i \"{payload.Input.FilePath}\"");

        // Video codec options
        if (payload.Profile.VideoProfile != null)
        {
            VideoProfileConfig video = payload.Profile.VideoProfile;
            options["-c:v"] = video.Codec;

            if (video.Bitrate > 0)
                options["-b:v"] = $"{video.Bitrate}k";

            if (video.Crf > 0)
                options["-crf"] = video.Crf.ToString();

            if (!string.IsNullOrEmpty(video.Preset))
                options["-preset"] = video.Preset;

            if (!string.IsNullOrEmpty(video.Profile))
                options["-profile:v"] = video.Profile;

            if (!string.IsNullOrEmpty(video.Tune))
                options["-tune"] = video.Tune;

            if (!string.IsNullOrEmpty(video.PixelFormat))
                options["-pix_fmt"] = video.PixelFormat;

            // Merge custom video options
            if (video.CustomOptions is { Count: > 0 })
            {
                foreach (KeyValuePair<string, dynamic> kvp in video.CustomOptions)
                {
                    options[$"-{kvp.Key}"] = kvp.Value?.ToString();
                }
            }
        }

        // Audio codec options
        if (payload.Profile.AudioProfile != null)
        {
            AudioProfileConfig audio = payload.Profile.AudioProfile;
            options["-c:a"] = audio.Codec;

            if (audio.Bitrate > 0)
                options["-b:a"] = $"{audio.Bitrate}k";

            if (audio.Channels > 0)
                options["-ac"] = audio.Channels.ToString();

            if (audio.SampleRate > 0)
                options["-ar"] = audio.SampleRate.ToString();

            // Merge custom audio options
            if (audio.CustomOptions is { Count: > 0 })
            {
                foreach (KeyValuePair<string, dynamic> kvp in audio.CustomOptions)
                {
                    options[$"-{kvp.Key}"] = kvp.Value?.ToString();
                }
            }
        }
        else
        {
            options["-an"] = null;  // No audio
        }

        // Subtitle codec options
        if (payload.Profile.SubtitleProfile != null)
        {
            SubtitleProfileConfig subtitle = payload.Profile.SubtitleProfile;
            options["-c:s"] = subtitle.Codec;
        }
        else
        {
            options["-sn"] = null;  // No subtitles
        }

        // Container-specific options
        string container = payload.Profile.Container.ToLower();
        if (container == "m3u8")
        {
            options["-f"] = "hls";
            options["-hls_time"] = "10";
            options["-hls_list_size"] = "0";
            options["-start_number"] = "0";
            options["-hls_segment_filename"] = $"\"{Path.Combine(payload.Output.DestinationFolder, "segment-%03d.ts")}\"";
        }
        else if (container == "mp4")
        {
            options["-f"] = "mp4";
            options["-movflags"] = "+faststart";  // Enable streaming
        }
        else if (container == "mkv")
        {
            options["-f"] = "matroska";
        }

        // Apply all options to command string
        foreach (KeyValuePair<string, string?> kvp in options)
        {
            sb.Append($" {kvp.Key}");
            if (kvp.Value != null)
            {
                sb.Append($" {kvp.Value}");
            }
        }

        // Output file
        string outputFile = Path.Combine(
            payload.Output.DestinationFolder,
            payload.Output.FileName
        );
        sb.Append($" \"{outputFile}\"");

        return sb.ToString();
    }

    /// <summary>
    /// Execute FFmpeg process with progress monitoring
    /// Updates Status.Progress as encoding proceeds
    /// </summary>
    private async Task ExecuteFfmpegAsync(EncodingJobPayload payload, string ffmpegCommand, CancellationToken cancellationToken)
    {
        // Parse FFmpeg path from command
        string ffmpegPath = AppFiles.FfmpegPath;

        // Extract arguments (remove 'ffmpeg ' prefix and quotes)
        string args = ffmpegCommand.Replace("ffmpeg ", "").Trim();

        ProcessStartInfo processInfo = new()
        {
            FileName = ffmpegPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process process = new() { StartInfo = processInfo };

        Task progressTask = Task.Run(() => MonitorProgress(payload, process), cancellationToken);

        process.Start();

        string error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"FFmpeg encoding failed: {error}");
        }

        await progressTask;
    }

    /// <summary>
    /// Monitor FFmpeg progress output and update status
    /// Parses frame, fps, bitrate from stderr output
    /// </summary>
    private void MonitorProgress(EncodingJobPayload payload, Process process)
    {
        Regex frameRegex = new(@"frame=\s*(\d+)");
        Regex fpsRegex = new(@"fps=\s*([\d.]+)");
        Regex bitrateRegex = new(@"bitrate=\s*([\d.]+)kbits/s");

        while (!process.StandardError.EndOfStream)
        {
            string? line = process.StandardError.ReadLine();
            if (string.IsNullOrEmpty(line)) continue;

            Match frameMatch = frameRegex.Match(line);
            if (frameMatch.Success && int.TryParse(frameMatch.Groups[1].Value, out int frame))
            {
                payload.Status.EncodedFrames = frame;
                long totalFrames = payload.Input.Duration.Ticks > 0 
                    ? (long)(payload.Input.Duration.TotalSeconds * 30)
                    : frame + 1;
                payload.Status.ProgressPercentage = (frame / (double)totalFrames) * 100;
            }

            Match fpsMatch = fpsRegex.Match(line);
            if (fpsMatch.Success && double.TryParse(fpsMatch.Groups[1].Value, out double fps))
            {
                payload.Status.Fps = fps;
            }

            Match bitrateMatch = bitrateRegex.Match(line);
            if (bitrateMatch.Success && double.TryParse(bitrateMatch.Groups[1].Value, out double bitrate))
            {
                payload.Status.CurrentBitrate = $"{bitrate}k";
            }
        }
    }

    /// <summary>
    /// Apply encoding job rules to determine post-processing actions
    /// Rules determine subtitle handling, audio selection, hardware acceleration, etc.
    /// </summary>
    private async Task<List<PostProcessingAction>> GetPostProcessingActionsAsync(EncodingJobPayload payload)
    {
        List<PostProcessingAction> actions = new();

        // Get IEncodingJobRule instances from DI (would be injected in real implementation)
        // For now, return empty - would be expanded with rule application

        return await Task.FromResult(actions);
    }

    /// <summary>
    /// Execute post-processing actions (font extraction, subtitle conversion, etc.)
    /// </summary>
    private async Task ExecutePostProcessingActionsAsync(EncodingJobPayload payload, List<PostProcessingAction> actions, CancellationToken cancellationToken)
    {
        foreach (PostProcessingAction action in actions)
        {
            switch (action.ActionType)
            {
                case "font-extraction":
                    // Extract fonts from ASS subtitles
                    await ExtractFontsAsync(action, cancellationToken);
                    break;

                case "subtitle-conversion":
                    // Convert image-based subtitles to WebVTT
                    await ConvertSubtitlesAsync(action, cancellationToken);
                    break;

                case "audio-stream-selection":
                    // Re-encode audio with language-based selection
                    await SelectAudioStreamsAsync(action, cancellationToken);
                    break;
            }
        }
    }

    private async Task ExtractFontsAsync(PostProcessingAction action, CancellationToken cancellationToken)
    {
        // Implementation would extract fonts from MKV attachments
        await Task.CompletedTask;
    }

    private async Task ConvertSubtitlesAsync(PostProcessingAction action, CancellationToken cancellationToken)
    {
        // Implementation would convert PGS/DVDSUB to WebVTT via OCR
        await Task.CompletedTask;
    }

    private async Task SelectAudioStreamsAsync(PostProcessingAction action, CancellationToken cancellationToken)
    {
        // Implementation would re-mux with selected audio streams
        await Task.CompletedTask;
    }

    /// <summary>
    /// Validate output file meets profile requirements
    /// </summary>
    private async Task ValidateOutputAsync(EncodingJobPayload payload)
    {
        string outputFile = Path.Combine(
            payload.Output.DestinationFolder,
            payload.Output.FileName
        );

        if (!File.Exists(outputFile))
        {
            throw new FileNotFoundException($"Output file not created: {outputFile}");
        }

        FileInfo fileInfo = new(outputFile);
        if (fileInfo.Length < 1024) // Minimum 1KB
        {
            throw new InvalidOperationException($"Output file too small: {fileInfo.Length} bytes");
        }

        payload.Status.OutputSize = fileInfo.Length;

        await Task.CompletedTask;
    }
}
