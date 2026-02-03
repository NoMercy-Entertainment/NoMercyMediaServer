using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using NoMercy.EncoderV2.Abstractions;
using NoMercy.NmSystem.Information;

namespace NoMercy.EncoderV2.Services;

/// <summary>
/// Default implementation of IFFmpegExecutor
/// </summary>
public sealed partial class FFmpegExecutor : IFFmpegExecutor
{
    private readonly Dictionary<int, Process> _runningProcesses = new();
    private readonly object _processLock = new();

    public string FFmpegPath { get; }
    public string FFprobePath { get; }

    public FFmpegExecutor()
    {
        FFmpegPath = AppFiles.FfmpegPath;
        FFprobePath = AppFiles.FfProbePath;
    }

    public FFmpegExecutor(string ffmpegPath, string ffprobePath)
    {
        FFmpegPath = ffmpegPath;
        FFprobePath = ffprobePath;
    }

    public async Task<FFmpegResult> ExecuteAsync(
        string arguments,
        string? workingDirectory = null,
        IProgress<EncodingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<string> outputLines = [];
        List<string> errorLines = [];
        DurationHolder durationHolder = new();

        ProcessStartInfo startInfo = new()
        {
            FileName = FFmpegPath,
            Arguments = $"-progress pipe:1 -nostats {arguments}",
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using Process process = new() { StartInfo = startInfo };

        try
        {
            process.Start();

            lock (_processLock)
            {
                _runningProcesses[process.Id] = process;
            }

            Task outputTask = ReadOutputAsync(process.StandardOutput, outputLines, progress, durationHolder, cancellationToken);
            Task errorTask = ReadErrorAsync(process.StandardError, errorLines, durationHolder, cancellationToken);

            using CancellationTokenRegistration registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // Ignore errors during cancellation
                }
            });

            await Task.WhenAll(outputTask, errorTask);
            await process.WaitForExitAsync(cancellationToken);

            stopwatch.Stop();

            return new FFmpegResult
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                StandardOutput = string.Join(Environment.NewLine, outputLines),
                StandardError = string.Join(Environment.NewLine, errorLines),
                Duration = stopwatch.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            return new FFmpegResult
            {
                Success = false,
                ExitCode = -1,
                StandardOutput = string.Join(Environment.NewLine, outputLines),
                StandardError = string.Join(Environment.NewLine, errorLines),
                Duration = stopwatch.Elapsed,
                Exception = new OperationCanceledException("Encoding was cancelled")
            };
        }
        catch (Exception ex)
        {
            return new FFmpegResult
            {
                Success = false,
                ExitCode = -1,
                StandardOutput = string.Join(Environment.NewLine, outputLines),
                StandardError = string.Join(Environment.NewLine, errorLines),
                Duration = stopwatch.Elapsed,
                Exception = ex
            };
        }
        finally
        {
            lock (_processLock)
            {
                _runningProcesses.Remove(process.Id);
            }
        }
    }

    public async Task<FFmpegResult> ExecuteSilentAsync(
        string arguments,
        CancellationToken cancellationToken = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        ProcessStartInfo startInfo = new()
        {
            FileName = FFmpegPath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using Process process = new() { StartInfo = startInfo };

        try
        {
            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            string error = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            stopwatch.Stop();

            return new FFmpegResult
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                StandardOutput = output,
                StandardError = error,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            return new FFmpegResult
            {
                Success = false,
                ExitCode = -1,
                Duration = stopwatch.Elapsed,
                Exception = ex
            };
        }
    }

    public Task<bool> PauseAsync(int processId)
    {
        return Task.Run(() =>
        {
            lock (_processLock)
            {
                if (!_runningProcesses.TryGetValue(processId, out Process? process) || process.HasExited)
                {
                    return false;
                }

                try
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        return SuspendProcessWindows(process);
                    }
                    else
                    {
                        return SendSignalUnix(process, "SIGSTOP");
                    }
                }
                catch
                {
                    return false;
                }
            }
        });
    }

    public Task<bool> ResumeAsync(int processId)
    {
        return Task.Run(() =>
        {
            lock (_processLock)
            {
                if (!_runningProcesses.TryGetValue(processId, out Process? process) || process.HasExited)
                {
                    return false;
                }

                try
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        return ResumeProcessWindows(process);
                    }
                    else
                    {
                        return SendSignalUnix(process, "SIGCONT");
                    }
                }
                catch
                {
                    return false;
                }
            }
        });
    }

    public Task<bool> CancelAsync(int processId)
    {
        return Task.Run(() =>
        {
            lock (_processLock)
            {
                if (!_runningProcesses.TryGetValue(processId, out Process? process) || process.HasExited)
                {
                    return false;
                }

                try
                {
                    process.Kill(entireProcessTree: true);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        });
    }

    private sealed class DurationHolder
    {
        public TimeSpan TotalDuration { get; set; } = TimeSpan.Zero;
    }

    private async Task ReadOutputAsync(
        StreamReader reader,
        List<string> outputLines,
        IProgress<EncodingProgress>? progress,
        DurationHolder durationHolder,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> progressData = new();

        while (!cancellationToken.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(cancellationToken);
            if (line == null) break;

            outputLines.Add(line);

            if (line.Contains('='))
            {
                string[] parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    progressData[parts[0].Trim()] = parts[1].Trim();
                }
            }

            if (line.StartsWith("progress=") && progress != null)
            {
                EncodingProgress encodingProgress = ParseProgress(progressData, durationHolder.TotalDuration);
                progress.Report(encodingProgress);
                progressData.Clear();
            }
        }
    }

    private async Task ReadErrorAsync(
        StreamReader reader,
        List<string> errorLines,
        DurationHolder durationHolder,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(cancellationToken);
            if (line == null) break;

            errorLines.Add(line);

            // Parse duration from stderr
            Match durationMatch = DurationRegex().Match(line);
            if (durationMatch.Success)
            {
                durationHolder.TotalDuration = TimeSpan.Parse(durationMatch.Groups[1].Value);
            }
        }
    }

    private static EncodingProgress ParseProgress(Dictionary<string, string> data, TimeSpan totalDuration)
    {
        double percentage = 0;
        TimeSpan elapsed = TimeSpan.Zero;
        TimeSpan? estimated = null;
        double? fps = null;
        double? bitrate = null;
        long? frame = null;
        string? speed = null;

        if (data.TryGetValue("out_time_ms", out string? outTimeMs) && long.TryParse(outTimeMs, out long ms))
        {
            elapsed = TimeSpan.FromMicroseconds(ms);
            if (totalDuration > TimeSpan.Zero)
            {
                percentage = Math.Min(100, elapsed.TotalSeconds / totalDuration.TotalSeconds * 100);
                if (percentage > 0)
                {
                    estimated = TimeSpan.FromSeconds((totalDuration.TotalSeconds - elapsed.TotalSeconds) / (percentage / 100));
                }
            }
        }

        if (data.TryGetValue("fps", out string? fpsStr) && double.TryParse(fpsStr, out double fpsVal))
        {
            fps = fpsVal;
        }

        if (data.TryGetValue("bitrate", out string? bitrateStr))
        {
            Match match = BitrateRegex().Match(bitrateStr);
            if (match.Success && double.TryParse(match.Groups[1].Value, out double bitrateVal))
            {
                bitrate = bitrateVal;
            }
        }

        if (data.TryGetValue("frame", out string? frameStr) && long.TryParse(frameStr, out long frameVal))
        {
            frame = frameVal;
        }

        if (data.TryGetValue("speed", out string? speedStr))
        {
            speed = speedStr;
        }

        return new EncodingProgress
        {
            Percentage = percentage,
            Elapsed = elapsed,
            Estimated = estimated,
            Fps = fps,
            Bitrate = bitrate,
            Frame = frame,
            Speed = speed
        };
    }

    private static bool SuspendProcessWindows(Process process)
    {
        // Windows-specific suspension via NtSuspendProcess
        // This is a simplified version - in production you'd use P/Invoke
        return false; // TODO: Implement Windows suspension
    }

    private static bool ResumeProcessWindows(Process process)
    {
        // Windows-specific resume via NtResumeProcess
        return false; // TODO: Implement Windows resume
    }

    private static bool SendSignalUnix(Process process, string signal)
    {
        try
        {
            using Process kill = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "kill",
                    Arguments = $"-{signal} {process.Id}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            kill.Start();
            kill.WaitForExit(1000);
            return kill.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    [GeneratedRegex(@"Duration:\s*(\d{2}:\d{2}:\d{2}\.\d{2})")]
    private static partial Regex DurationRegex();

    [GeneratedRegex(@"([\d.]+)kbits/s")]
    private static partial Regex BitrateRegex();
}
