using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NoMercy.EncoderNode.Configuration;

namespace NoMercy.EncoderNode.Services;

/// <summary>
/// Progress data parsed from FFmpeg output
/// </summary>
public class EncodingProgressData
{
    [JsonProperty("task_id")]
    public string TaskId { get; set; } = string.Empty;

    [JsonProperty("job_id")]
    public string JobId { get; set; } = string.Empty;

    [JsonProperty("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonProperty("progress_percentage")]
    public double ProgressPercentage { get; set; }

    [JsonProperty("encoded_frames")]
    public long EncodedFrames { get; set; }

    [JsonProperty("total_frames")]
    public long? TotalFrames { get; set; }

    [JsonProperty("fps")]
    public double Fps { get; set; }

    [JsonProperty("speed")]
    public double Speed { get; set; }

    [JsonProperty("bitrate")]
    public string Bitrate { get; set; } = string.Empty;

    [JsonProperty("current_time")]
    public TimeSpan CurrentTime { get; set; }

    [JsonProperty("total_duration")]
    public TimeSpan TotalDuration { get; set; }

    [JsonProperty("estimated_remaining")]
    public TimeSpan EstimatedRemaining { get; set; }

    [JsonProperty("output_size")]
    public long OutputSize { get; set; }

    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Configuration options for progress emission
/// </summary>
public class ProgressEmitterOptions
{
    /// <summary>
    /// Minimum interval between progress reports to the server (in milliseconds)
    /// Default: 1000ms (1 second)
    /// </summary>
    public int ThrottleIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Whether to log progress to console
    /// </summary>
    public bool LogProgress { get; set; } = true;

    /// <summary>
    /// Whether to send progress to the server
    /// </summary>
    public bool SendToServer { get; set; } = true;

    /// <summary>
    /// Timeout for server requests in milliseconds
    /// </summary>
    public int RequestTimeoutMs { get; set; } = 5000;
}

/// <summary>
/// Interface for emitting encoding progress to the primary server
/// </summary>
public interface IProgressEmitter
{
    /// <summary>
    /// Parse FFmpeg progress output line and emit progress if changed significantly
    /// </summary>
    Task ParseAndEmitAsync(string taskId, string jobId, string ffmpegOutput, TimeSpan totalDuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Emit progress data directly
    /// </summary>
    Task EmitProgressAsync(EncodingProgressData progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a progress callback for use with FFmpeg process
    /// </summary>
    Action<string> CreateProgressCallback(string taskId, string jobId, TimeSpan totalDuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Parse FFmpeg output line to extract progress data
    /// </summary>
    EncodingProgressData? ParseProgressLine(string output, TimeSpan totalDuration);

    /// <summary>
    /// Report task started
    /// </summary>
    Task ReportTaskStartedAsync(string taskId, string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Report task completed
    /// </summary>
    Task ReportTaskCompletedAsync(string taskId, string jobId, bool success, string? errorMessage = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for emitting encoding progress to the primary NoMercy MediaServer.
/// Parses FFmpeg output, throttles updates, and sends progress via HTTP.
/// </summary>
public class ProgressEmitter : IProgressEmitter
{
    private readonly ILogger<ProgressEmitter> _logger;
    private readonly HttpClient _httpClient;
    private readonly IKeycloakAuthService _keycloakAuth;
    private readonly IServerDiscoveryService _serverDiscovery;
    private readonly IOptions<EncoderNodeOptions> _options;
    private readonly ProgressEmitterOptions _emitterOptions;
    private readonly string _nodeId;
    private string _primaryServerUrl = string.Empty;

    // Throttling state per task
    private readonly Dictionary<string, DateTime> _lastEmitTime = new();
    private readonly Dictionary<string, EncodingProgressData> _lastProgress = new();
    private readonly object _throttleLock = new();

    // Regex patterns for FFmpeg output parsing
    private static readonly Regex FrameRegex = new(@"frame=\s*(\d+)", RegexOptions.Compiled);
    private static readonly Regex FpsRegex = new(@"fps=\s*([\d.]+)", RegexOptions.Compiled);
    private static readonly Regex SpeedRegex = new(@"speed=\s*([\d.]+)x", RegexOptions.Compiled);
    private static readonly Regex BitrateRegex = new(@"bitrate=\s*([\d.]+\s*\w+)", RegexOptions.Compiled);
    private static readonly Regex TimeRegex = new(@"time=\s*(\d{2}:\d{2}:\d{2}(?:\.\d+)?)", RegexOptions.Compiled);
    private static readonly Regex SizeRegex = new(@"size=\s*(\d+)\s*(\w+)", RegexOptions.Compiled);

    // Progress stat format (used with -progress pipe:1)
    private static readonly Regex ProgressStatRegex = new(@"^(\w+)=(.+)$", RegexOptions.Compiled);

    public ProgressEmitter(
        ILogger<ProgressEmitter> logger,
        HttpClient httpClient,
        IKeycloakAuthService keycloakAuth,
        IServerDiscoveryService serverDiscovery,
        IOptions<EncoderNodeOptions> options)
    {
        _logger = logger;
        _httpClient = httpClient;
        _keycloakAuth = keycloakAuth;
        _serverDiscovery = serverDiscovery;
        _options = options;

        // Load options from configuration or use defaults
        _emitterOptions = new ProgressEmitterOptions
        {
            ThrottleIntervalMs = options.Value.Encoder.ProgressReportIntervalMs > 0
                ? options.Value.Encoder.ProgressReportIntervalMs
                : 1000,
            LogProgress = options.Value.Encoder.LogProgress,
            SendToServer = true
        };

        // Get node ID
        _nodeId = EncoderNodeAppFiles.GetNodeIdAsync().GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async Task ParseAndEmitAsync(string taskId, string jobId, string ffmpegOutput, TimeSpan totalDuration, CancellationToken cancellationToken = default)
    {
        EncodingProgressData? progress = ParseProgressLine(ffmpegOutput, totalDuration);
        if (progress == null) return;

        progress.TaskId = taskId;
        progress.JobId = jobId;
        progress.NodeId = _nodeId;

        await EmitProgressAsync(progress, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task EmitProgressAsync(EncodingProgressData progress, CancellationToken cancellationToken = default)
    {
        // Check throttling
        if (!ShouldEmit(progress.TaskId, progress))
        {
            return;
        }

        // Log progress if enabled
        if (_emitterOptions.LogProgress)
        {
            _logger.LogDebug(
                "Task {TaskId}: {Progress:F1}% @ {Fps:F1}fps, {Speed:F2}x speed, {Bitrate}",
                progress.TaskId,
                progress.ProgressPercentage,
                progress.Fps,
                progress.Speed,
                progress.Bitrate);
        }

        // Send to server if enabled
        if (_emitterOptions.SendToServer)
        {
            await SendProgressToServerAsync(progress, cancellationToken);
        }

        // Update last emit state
        lock (_throttleLock)
        {
            _lastEmitTime[progress.TaskId] = DateTime.UtcNow;
            _lastProgress[progress.TaskId] = progress;
        }
    }

    /// <inheritdoc/>
    public Action<string> CreateProgressCallback(string taskId, string jobId, TimeSpan totalDuration, CancellationToken cancellationToken = default)
    {
        // Track progress stats from -progress output
        Dictionary<string, string> progressStats = new();

        return (string output) =>
        {
            if (string.IsNullOrEmpty(output)) return;

            // Try to parse as progress stat line (from -progress pipe:1)
            Match statMatch = ProgressStatRegex.Match(output);
            if (statMatch.Success)
            {
                string key = statMatch.Groups[1].Value;
                string value = statMatch.Groups[2].Value;
                progressStats[key] = value;

                // When we get "progress" line, we have a complete set
                if (key == "progress")
                {
                    EncodingProgressData? progress = ParseProgressStats(progressStats, totalDuration);
                    if (progress != null)
                    {
                        progress.TaskId = taskId;
                        progress.JobId = jobId;
                        progress.NodeId = _nodeId;
                        _ = EmitProgressAsync(progress, cancellationToken);
                    }
                    progressStats.Clear();
                }
                return;
            }

            // Otherwise try standard FFmpeg output line parsing
            EncodingProgressData? lineProgress = ParseProgressLine(output, totalDuration);
            if (lineProgress != null)
            {
                lineProgress.TaskId = taskId;
                lineProgress.JobId = jobId;
                lineProgress.NodeId = _nodeId;
                _ = EmitProgressAsync(lineProgress, cancellationToken);
            }
        };
    }

    /// <inheritdoc/>
    public EncodingProgressData? ParseProgressLine(string output, TimeSpan totalDuration)
    {
        if (string.IsNullOrEmpty(output)) return null;

        // Check if this line contains frame info (standard FFmpeg stderr output)
        if (!output.Contains("frame=", StringComparison.OrdinalIgnoreCase) &&
            !output.Contains("time=", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        EncodingProgressData progress = new()
        {
            TotalDuration = totalDuration,
            Timestamp = DateTime.UtcNow
        };

        // Parse frame count
        Match frameMatch = FrameRegex.Match(output);
        if (frameMatch.Success && long.TryParse(frameMatch.Groups[1].Value, out long frame))
        {
            progress.EncodedFrames = frame;
        }

        // Parse FPS
        Match fpsMatch = FpsRegex.Match(output);
        if (fpsMatch.Success && double.TryParse(fpsMatch.Groups[1].Value, out double fps))
        {
            progress.Fps = fps;
        }

        // Parse speed
        Match speedMatch = SpeedRegex.Match(output);
        if (speedMatch.Success && double.TryParse(speedMatch.Groups[1].Value, out double speed))
        {
            progress.Speed = speed;
        }

        // Parse bitrate
        Match bitrateMatch = BitrateRegex.Match(output);
        if (bitrateMatch.Success)
        {
            progress.Bitrate = bitrateMatch.Groups[1].Value.Trim();
        }

        // Parse current time
        Match timeMatch = TimeRegex.Match(output);
        if (timeMatch.Success && TimeSpan.TryParse(timeMatch.Groups[1].Value, out TimeSpan currentTime))
        {
            progress.CurrentTime = currentTime;

            // Calculate progress percentage
            if (totalDuration.TotalSeconds > 0)
            {
                progress.ProgressPercentage = Math.Min(100.0, (currentTime.TotalSeconds / totalDuration.TotalSeconds) * 100.0);
            }

            // Calculate estimated remaining time
            if (progress.Speed > 0)
            {
                double remainingSeconds = (totalDuration - currentTime).TotalSeconds / progress.Speed;
                progress.EstimatedRemaining = TimeSpan.FromSeconds(Math.Max(0, remainingSeconds));
            }
        }

        // Parse output size
        Match sizeMatch = SizeRegex.Match(output);
        if (sizeMatch.Success && long.TryParse(sizeMatch.Groups[1].Value, out long size))
        {
            string unit = sizeMatch.Groups[2].Value.ToLowerInvariant();
            progress.OutputSize = unit switch
            {
                "kb" or "kib" => size * 1024,
                "mb" or "mib" => size * 1024 * 1024,
                "gb" or "gib" => size * 1024 * 1024 * 1024,
                _ => size
            };
        }

        return progress;
    }

    /// <inheritdoc/>
    public async Task ReportTaskStartedAsync(string taskId, string jobId, CancellationToken cancellationToken = default)
    {
        try
        {
            string url = await GetServerUrlAsync($"/api/v1/encoder/tasks/{taskId}/started");
            if (string.IsNullOrEmpty(url)) return;

            using HttpRequestMessage request = new(HttpMethod.Post, url);
            await AddAuthHeaderAsync(request, cancellationToken);

            var payload = new { jobId, nodeId = _nodeId, startedAt = DateTime.UtcNow };
            request.Content = new StringContent(
                JsonConvert.SerializeObject(payload),
                System.Text.Encoding.UTF8,
                "application/json");

            HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Task {TaskId} started notification sent", taskId);
            }
            else
            {
                _logger.LogWarning("Failed to send task started notification: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reporting task {TaskId} started", taskId);
        }
    }

    /// <inheritdoc/>
    public async Task ReportTaskCompletedAsync(string taskId, string jobId, bool success, string? errorMessage = null, CancellationToken cancellationToken = default)
    {
        try
        {
            string url = await GetServerUrlAsync($"/api/v1/encoder/tasks/{taskId}/completed");
            if (string.IsNullOrEmpty(url)) return;

            using HttpRequestMessage request = new(HttpMethod.Post, url);
            await AddAuthHeaderAsync(request, cancellationToken);

            var payload = new
            {
                jobId,
                nodeId = _nodeId,
                success,
                errorMessage,
                completedAt = DateTime.UtcNow
            };
            request.Content = new StringContent(
                JsonConvert.SerializeObject(payload),
                System.Text.Encoding.UTF8,
                "application/json");

            HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Task {TaskId} completed notification sent (success={Success})", taskId, success);
            }
            else
            {
                _logger.LogWarning("Failed to send task completed notification: {StatusCode}", response.StatusCode);
            }

            // Clean up throttle state
            lock (_throttleLock)
            {
                _lastEmitTime.Remove(taskId);
                _lastProgress.Remove(taskId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reporting task {TaskId} completed", taskId);
        }
    }

    /// <summary>
    /// Parse progress from FFmpeg -progress output stats
    /// </summary>
    private EncodingProgressData? ParseProgressStats(Dictionary<string, string> stats, TimeSpan totalDuration)
    {
        EncodingProgressData progress = new()
        {
            TotalDuration = totalDuration,
            Timestamp = DateTime.UtcNow
        };

        // Parse frame
        if (stats.TryGetValue("frame", out string? frameStr) && long.TryParse(frameStr, out long frame))
        {
            progress.EncodedFrames = frame;
        }

        // Parse fps
        if (stats.TryGetValue("fps", out string? fpsStr) && double.TryParse(fpsStr, out double fps))
        {
            progress.Fps = fps;
        }

        // Parse speed
        if (stats.TryGetValue("speed", out string? speedStr))
        {
            speedStr = speedStr.TrimEnd('x');
            if (double.TryParse(speedStr, out double speed))
            {
                progress.Speed = speed;
            }
        }

        // Parse bitrate
        if (stats.TryGetValue("bitrate", out string? bitrate))
        {
            progress.Bitrate = bitrate;
        }

        // Parse out_time (microseconds)
        if (stats.TryGetValue("out_time_us", out string? outTimeUs) && long.TryParse(outTimeUs, out long microseconds))
        {
            progress.CurrentTime = TimeSpan.FromMicroseconds(microseconds);
        }
        else if (stats.TryGetValue("out_time", out string? outTime) && TimeSpan.TryParse(outTime, out TimeSpan parsedTime))
        {
            progress.CurrentTime = parsedTime;
        }

        // Parse total_size
        if (stats.TryGetValue("total_size", out string? sizeStr) && long.TryParse(sizeStr, out long size))
        {
            progress.OutputSize = size;
        }

        // Calculate progress percentage
        if (totalDuration.TotalSeconds > 0 && progress.CurrentTime.TotalSeconds > 0)
        {
            progress.ProgressPercentage = Math.Min(100.0, (progress.CurrentTime.TotalSeconds / totalDuration.TotalSeconds) * 100.0);
        }

        // Calculate estimated remaining
        if (progress.Speed > 0 && totalDuration.TotalSeconds > 0)
        {
            double remainingSeconds = (totalDuration - progress.CurrentTime).TotalSeconds / progress.Speed;
            progress.EstimatedRemaining = TimeSpan.FromSeconds(Math.Max(0, remainingSeconds));
        }

        return progress;
    }

    /// <summary>
    /// Check if we should emit progress (throttling)
    /// </summary>
    private bool ShouldEmit(string taskId, EncodingProgressData progress)
    {
        lock (_throttleLock)
        {
            // Always emit if first progress report
            if (!_lastEmitTime.TryGetValue(taskId, out DateTime lastTime))
            {
                return true;
            }

            // Check time throttle
            TimeSpan elapsed = DateTime.UtcNow - lastTime;
            if (elapsed.TotalMilliseconds < _emitterOptions.ThrottleIntervalMs)
            {
                return false;
            }

            // Always emit if progress jumped significantly (> 1%)
            if (_lastProgress.TryGetValue(taskId, out EncodingProgressData? lastProgress))
            {
                if (Math.Abs(progress.ProgressPercentage - lastProgress.ProgressPercentage) >= 1.0)
                {
                    return true;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Send progress data to the primary server
    /// </summary>
    private async Task SendProgressToServerAsync(EncodingProgressData progress, CancellationToken cancellationToken)
    {
        try
        {
            string url = await GetServerUrlAsync($"/api/v1/encoder/tasks/{progress.TaskId}/progress");
            if (string.IsNullOrEmpty(url)) return;

            using HttpRequestMessage request = new(HttpMethod.Post, url);
            await AddAuthHeaderAsync(request, cancellationToken);

            request.Content = new StringContent(
                JsonConvert.SerializeObject(progress),
                System.Text.Encoding.UTF8,
                "application/json");

            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_emitterOptions.RequestTimeoutMs);

            HttpResponseMessage response = await _httpClient.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Progress report failed: {StatusCode}", response.StatusCode);
            }
        }
        catch (TaskCanceledException)
        {
            // Timeout - don't log as error, just skip this update
            _logger.LogDebug("Progress report timed out for task {TaskId}", progress.TaskId);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to send progress for task {TaskId}", progress.TaskId);
        }
    }

    /// <summary>
    /// Get the full server URL for an endpoint
    /// </summary>
    private async Task<string> GetServerUrlAsync(string endpoint)
    {
        if (string.IsNullOrEmpty(_primaryServerUrl))
        {
            // Try discovered URL first
            string? discovered = _serverDiscovery.GetPrimaryServerUrl();
            if (!string.IsNullOrEmpty(discovered))
            {
                _primaryServerUrl = discovered;
            }
            else
            {
                _primaryServerUrl = _options.Value.PrimaryServer.Url;
            }
        }

        if (string.IsNullOrEmpty(_primaryServerUrl))
        {
            _logger.LogWarning("Primary server URL not configured");
            return string.Empty;
        }

        return $"{_primaryServerUrl.TrimEnd('/')}{endpoint}";
    }

    /// <summary>
    /// Add authentication header to request
    /// </summary>
    private async Task AddAuthHeaderAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Try user token first
        string? token = EncoderNodeAuth.GetAccessToken();

        // Fall back to Keycloak service token
        if (string.IsNullOrEmpty(token) && _options.Value.Keycloak.Enabled)
        {
            try
            {
                token = await _keycloakAuth.GetAccessTokenAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not get Keycloak token for progress report");
            }
        }

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
    }
}
