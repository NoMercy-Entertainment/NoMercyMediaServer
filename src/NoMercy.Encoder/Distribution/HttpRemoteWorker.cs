namespace NoMercy.Encoder.Distribution;

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;

/// <summary>
/// HTTP-based remote worker. Posts signed <see cref="EncodeTask"/>
/// payloads to the worker's /api/v1/worker/execute-task endpoint and
/// unwraps the signed <see cref="DispatchResult"/> response. Capabilities
/// and budget snapshots are cached in memory from the last handshake /
/// heartbeat — the caller (registry / heartbeat service) refreshes them
/// out of band so hot dispatch paths don't block on network I/O.
///
/// Designed to be straightforward to mock: all network calls go through
/// the injected <see cref="HttpClient"/>. Tests replace it with a
/// <see cref="HttpMessageHandler"/>-backed fake to drive every success
/// / failure path without standing up a real server.
/// </summary>
public class HttpRemoteWorker : IRemoteWorker
{
    private readonly HttpClient _http;
    private readonly ITaskSerializer _serializer;
    private readonly byte[] _signingKey;
    private readonly ILogger<HttpRemoteWorker> _logger;

    // Mutable: registry / heartbeat service pushes the latest values.
    private IHardwareCapabilities _capabilities;
    private ResourceBudgetSnapshot _budget;

    public string WorkerId { get; }

    public HttpRemoteWorker(
        string workerId,
        HttpClient http,
        ITaskSerializer serializer,
        byte[] signingKey,
        IHardwareCapabilities initialCapabilities,
        ResourceBudgetSnapshot initialBudget,
        ILogger<HttpRemoteWorker> logger
    )
    {
        WorkerId = workerId;
        _http = http;
        _serializer = serializer;
        _signingKey = signingKey;
        _capabilities = initialCapabilities;
        _budget = initialBudget;
        _logger = logger;
    }

    /// <summary>
    /// Called by the heartbeat service when a worker reports its latest
    /// resource availability. Thread-safe via reference assignment — the
    /// dispatcher reads a snapshot, the heartbeat replaces the reference.
    /// </summary>
    public void UpdateSnapshot(IHardwareCapabilities capabilities, ResourceBudgetSnapshot budget)
    {
        _capabilities = capabilities;
        _budget = budget;
    }

    public IHardwareCapabilities GetCapabilities() => _capabilities;

    public ResourceBudgetSnapshot GetAvailableBudget() => _budget;

    public async Task<DispatchResult> ExecuteTaskAsync(EncodeTask task, CancellationToken ct)
    {
        string payload = _serializer.Serialize(task, _signingKey);
        HttpContent content = new StringContent(payload, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http
                .PostAsync("api/v1/worker/execute-task", content, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "HTTP error dispatching task {TaskId} to worker {WorkerId}",
                task.TaskId,
                WorkerId
            );
            return new(
                TaskId: task.TaskId,
                Success: false,
                OutputPath: task.OutputPath,
                Duration: TimeSpan.Zero,
                Error: $"HTTP error: {ex.Message}",
                WorkerId: WorkerId
            );
        }

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            _logger.LogWarning(
                "Worker {WorkerId} returned {StatusCode} for task {TaskId}: {Body}",
                WorkerId,
                (int)response.StatusCode,
                task.TaskId,
                Truncate(body, 500)
            );
            return new(
                TaskId: task.TaskId,
                Success: false,
                OutputPath: task.OutputPath,
                Duration: TimeSpan.Zero,
                Error: $"Worker returned HTTP {(int)response.StatusCode}",
                WorkerId: WorkerId
            );
        }

        string responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        DispatchResult? result = _serializer.DeserializeResult(responseBody, _signingKey);
        if (result is null)
        {
            _logger.LogWarning(
                "Worker {WorkerId} returned an unsignable / expired response for task {TaskId}",
                WorkerId,
                task.TaskId
            );
            return new(
                TaskId: task.TaskId,
                Success: false,
                OutputPath: task.OutputPath,
                Duration: TimeSpan.Zero,
                Error: "Worker response failed HMAC verification",
                WorkerId: WorkerId
            );
        }

        // Always stamp WorkerId on the returned result so the orchestrator
        // can attribute the work to this specific worker even if the
        // worker's own serialized DispatchResult omitted it.
        return result with
        {
            WorkerId = WorkerId,
        };
    }

    // Legacy job-level path — kept until all callers migrate to task-level.
    // Currently not invoked in the dispatch flow; returns unsupported.
    public Task<RemoteEncodingResult> ExecuteJobAsync(
        EncodingJob job,
        IProgress<EncodingProgress> progress,
        CancellationToken ct
    )
    {
        _logger.LogWarning(
            "ExecuteJobAsync called on HttpRemoteWorker {WorkerId} — job-level remote dispatch is not supported; use task-level ExecuteTaskAsync",
            WorkerId
        );
        return Task.FromResult(
            new RemoteEncodingResult(
                Success: false,
                WorkerId: WorkerId,
                OutputPath: string.Empty,
                Duration: TimeSpan.Zero,
                Error: new(
                    Kind: EncodingErrorKind.Unknown,
                    Message: "Job-level remote dispatch not supported — use task-level",
                    FfmpegStderr: null,
                    StageName: null,
                    Recoverable: false
                ),
                Metrics: null
            )
        );
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
