using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.EncoderV2.Progress;

/// <summary>
/// Interface for SignalR broadcasting to decouple from API layer
/// </summary>
public interface ISignalRBroadcaster
{
    Task BroadcastProgressAsync(string jobId, string taskId, EncodingProgressInfo progress);
    Task BroadcastJobStateChangeAsync(string jobId, string previousState, string newState, string? errorMessage);
    Task BroadcastTaskStateChangeAsync(string jobId, string taskId, string taskType, string previousState, string newState, string? errorMessage);
}

/// <summary>
/// Monitors and reports encoding progress
/// Parses FFmpeg output and updates database/SignalR
/// </summary>
public partial class ProgressMonitor(QueueContext queueContext, ISignalRBroadcaster? signalRBroadcaster = null) : IProgressMonitor
{
    private readonly QueueContext _queueContext = queueContext;
    private readonly ISignalRBroadcaster? _signalRBroadcaster = signalRBroadcaster;

    // Throttle SignalR updates to avoid overwhelming clients
    private DateTime _lastSignalRUpdate = DateTime.MinValue;
    private static readonly TimeSpan SignalRThrottleInterval = TimeSpan.FromMilliseconds(500);

    [GeneratedRegex(@"frame=\s*(\d+)")]
    private static partial Regex FrameRegex();

    [GeneratedRegex(@"fps=\s*([\d.]+)")]
    private static partial Regex FpsRegex();

    [GeneratedRegex(@"speed=\s*([\d.]+)x")]
    private static partial Regex SpeedRegex();

    [GeneratedRegex(@"bitrate=\s*([\d.]+\w+/s)")]
    private static partial Regex BitrateRegex();

    [GeneratedRegex(@"out_time=(\d{2}):(\d{2}):(\d{2})\.(\d+)")]
    private static partial Regex TimeRegex();

    public EncodingProgressInfo? ParseProgressOutput(string output, TimeSpan totalDuration)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        EncodingProgressInfo progress = new()
        {
            TotalDuration = totalDuration
        };

        Match frameMatch = FrameRegex().Match(output);
        if (frameMatch.Success)
        {
            progress.CurrentFrame = long.Parse(frameMatch.Groups[1].Value);
        }

        Match fpsMatch = FpsRegex().Match(output);
        if (fpsMatch.Success)
        {
            progress.Fps = double.Parse(fpsMatch.Groups[1].Value);
        }

        Match speedMatch = SpeedRegex().Match(output);
        if (speedMatch.Success)
        {
            progress.Speed = double.Parse(speedMatch.Groups[1].Value);
        }

        Match bitrateMatch = BitrateRegex().Match(output);
        if (bitrateMatch.Success)
        {
            progress.Bitrate = bitrateMatch.Groups[1].Value;
        }

        Match timeMatch = TimeRegex().Match(output);
        if (timeMatch.Success)
        {
            int hours = int.Parse(timeMatch.Groups[1].Value);
            int minutes = int.Parse(timeMatch.Groups[2].Value);
            int seconds = int.Parse(timeMatch.Groups[3].Value);
            int milliseconds = int.Parse(timeMatch.Groups[4].Value);

            progress.CurrentTime = new TimeSpan(0, hours, minutes, seconds, milliseconds);

            if (totalDuration.TotalSeconds > 0)
            {
                progress.ProgressPercentage = progress.CurrentTime.TotalSeconds / totalDuration.TotalSeconds * 100;
                progress.ProgressPercentage = Math.Min(100, Math.Max(0, progress.ProgressPercentage));

                if (progress.Speed > 0)
                {
                    double remainingSeconds = (totalDuration.TotalSeconds - progress.CurrentTime.TotalSeconds) / progress.Speed;
                    progress.EstimatedRemaining = TimeSpan.FromSeconds(remainingSeconds);
                }
            }
        }

        return progress;
    }

    public async Task ReportProgressAsync(string taskId, EncodingProgressInfo progress)
    {
        await ReportProgressAsync(string.Empty, taskId, progress);
    }

    public async Task ReportProgressAsync(string jobId, string taskId, EncodingProgressInfo progress)
    {
        progress.TaskId = taskId;

        // Save to database
        EncodingProgress dbProgress = new()
        {
            TaskId = taskId,
            ProgressPercentage = progress.ProgressPercentage,
            CurrentFrame = progress.CurrentFrame,
            Fps = progress.Fps,
            Speed = progress.Speed,
            Bitrate = progress.Bitrate,
            CurrentTime = progress.CurrentTime,
            EstimatedRemaining = progress.EstimatedRemaining,
            RecordedAt = DateTime.UtcNow
        };

        _queueContext.EncodingProgress.Add(dbProgress);
        await _queueContext.SaveChangesAsync();

        // Send to SignalR hub for real-time updates (throttled)
        if (_signalRBroadcaster != null && ShouldBroadcast())
        {
            _lastSignalRUpdate = DateTime.UtcNow;
            await _signalRBroadcaster.BroadcastProgressAsync(jobId, taskId, progress);
        }
    }

    public async Task<EncodingProgressInfo?> GetLatestProgressAsync(string taskId)
    {
        EncodingProgress? latest = await _queueContext.EncodingProgress
            .Where(p => p.TaskId == taskId)
            .OrderByDescending(p => p.RecordedAt)
            .FirstOrDefaultAsync();

        if (latest == null)
        {
            return null;
        }

        return new EncodingProgressInfo
        {
            TaskId = latest.TaskId,
            ProgressPercentage = latest.ProgressPercentage,
            CurrentFrame = latest.CurrentFrame,
            Fps = latest.Fps,
            Speed = latest.Speed,
            Bitrate = latest.Bitrate,
            CurrentTime = latest.CurrentTime,
            EstimatedRemaining = latest.EstimatedRemaining,
            Timestamp = latest.RecordedAt
        };
    }

    public async Task ReportJobStateChangeAsync(string jobId, string previousState, string newState, string? errorMessage = null)
    {
        if (_signalRBroadcaster != null)
        {
            await _signalRBroadcaster.BroadcastJobStateChangeAsync(jobId, previousState, newState, errorMessage);
        }
    }

    public async Task ReportTaskStateChangeAsync(string jobId, string taskId, string taskType, string previousState, string newState, string? errorMessage = null)
    {
        if (_signalRBroadcaster != null)
        {
            await _signalRBroadcaster.BroadcastTaskStateChangeAsync(jobId, taskId, taskType, previousState, newState, errorMessage);
        }
    }

    private bool ShouldBroadcast()
    {
        return DateTime.UtcNow - _lastSignalRUpdate >= SignalRThrottleInterval;
    }
}
