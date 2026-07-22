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

using System.Text;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Progress;

namespace NoMercy.Encoder.Distribution;

/// <summary>
/// HTTP-based remote worker. Posts signed <see cref="EncodeTask"/>
/// payloads to the worker's /api/v1/worker/tasks endpoint and
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
    private readonly HmacSigner? _hmacSigner;
    private readonly ILogger<HttpRemoteWorker> _logger;

    // Mutable: registry / heartbeat service pushes the latest values.
    private IHardwareCapabilities _capabilities;
    private ResourceBudgetSnapshot _budget;

    public string WorkerId { get; }

    /// <summary>
    /// The base URL this worker was registered with. Used by
    /// <see cref="JsonRemoteWorkerRegistry"/> to persist and rehydrate
    /// the worker entry across coordinator restarts.
    /// </summary>
    public string BaseUrl => _http.BaseAddress?.ToString() ?? string.Empty;

    public HttpRemoteWorker(
        string workerId,
        HttpClient http,
        ITaskSerializer serializer,
        byte[] signingKey,
        IHardwareCapabilities initialCapabilities,
        ResourceBudgetSnapshot initialBudget,
        ILogger<HttpRemoteWorker> logger,
        EncoderOptions? encoderOptions = null
    )
    {
        WorkerId = workerId;
        _http = http;
        _serializer = serializer;
        _signingKey = signingKey;
        _capabilities = initialCapabilities;
        _budget = initialBudget;
        _logger = logger;

        if (encoderOptions?.IsDistributedEncodingEnabled == true)
            _hmacSigner = new(secret: encoderOptions.DistributedEncodingSigningKey!);
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
        string payload = _serializer.Serialize(task: task, signingKey: _signingKey);
        byte[] bodyBytes = Encoding.UTF8.GetBytes(s: payload);
        HttpContent content = new ByteArrayContent(content: bodyBytes);
        content.Headers.ContentType = new(mediaType: "application/json") { CharSet = "utf-8" };

        const string path = "api/v1/worker/tasks";
        HttpRequestMessage request = new(method: HttpMethod.Post, requestUri: path) { Content = content };

        if (_hmacSigner is not null)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string signature = _hmacSigner.Sign(method: "POST", path: "/" + path, timestamp: timestamp, body: bodyBytes);
            request.Headers.Add(name: "X-NoMercy-Timestamp", value: timestamp.ToString());
            request.Headers.Add(name: "X-NoMercy-Signature", value: signature);
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request: request, cancellationToken: ct).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                exception: ex,
                message: "HTTP error dispatching task {TaskId} to worker {WorkerId}", args: [task.TaskId, WorkerId]
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
            string body = await response.Content.ReadAsStringAsync(cancellationToken: ct).ConfigureAwait(continueOnCapturedContext: false);
            _logger.LogWarning(
                message: "Worker {WorkerId} returned {StatusCode} for task {TaskId}: {Body}", args: [WorkerId, (int)response.StatusCode, task.TaskId, Truncate(s: body, max: 500)]
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

        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken: ct).ConfigureAwait(continueOnCapturedContext: false);
        DispatchResult? result = _serializer.DeserializeResult(payload: responseBody, signingKey: _signingKey);
        if (result is null)
        {
            _logger.LogWarning(
                message: "Worker {WorkerId} returned an unsignable / expired response for task {TaskId}", args: [WorkerId, task.TaskId]
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
            message: "ExecuteJobAsync called on HttpRemoteWorker {WorkerId} — job-level remote dispatch is not supported; use task-level ExecuteTaskAsync",
            args: WorkerId
        );
        return Task.FromResult(
            result: new RemoteEncodingResult(
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
