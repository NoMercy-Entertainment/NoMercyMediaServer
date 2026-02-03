using System.Diagnostics;
using NoMercy.NmSystem.Information;

namespace NoMercy.EncoderV2.FFmpeg;

/// <summary>
/// Service for executing FFmpeg commands with proper process management
/// </summary>
public class FFmpegService : IFFmpegService
{
    public async Task<FFmpegExecutionResult> ExecuteAsync(
        string command,
        string workingDirectory,
        Action<string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        FFmpegExecutionResult result = new();

        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = GetFFmpegPath(),
                Arguments = command,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using Process process = new() { StartInfo = startInfo };

            List<string> outputLines = [];
            List<string> errorLines = [];

            process.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    outputLines.Add(args.Data);
                }
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    errorLines.Add(args.Data);
                    progressCallback?.Invoke(args.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            stopwatch.Stop();

            result.ExitCode = process.ExitCode;
            result.Success = process.ExitCode == 0;
            result.StandardOutput = string.Join(Environment.NewLine, outputLines);
            result.StandardError = string.Join(Environment.NewLine, errorLines);
            result.ExecutionTime = stopwatch.Elapsed;

            if (!result.Success)
            {
                result.ErrorMessage = $"FFmpeg exited with code {process.ExitCode}";
            }
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.ErrorMessage = "FFmpeg execution was cancelled";
            result.ExecutionTime = stopwatch.Elapsed;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"FFmpeg execution failed: {ex.Message}";
            result.ExecutionTime = stopwatch.Elapsed;
        }

        return result;
    }

    public string GetFFmpegPath()
    {
        return AppFiles.FfmpegPath;
    }

    public async Task<bool> VerifyFFmpegAsync()
    {
        try
        {
            FFmpegExecutionResult result = await ExecuteAsync("-version", Environment.CurrentDirectory);
            return result.Success;
        }
        catch
        {
            return false;
        }
    }
}
