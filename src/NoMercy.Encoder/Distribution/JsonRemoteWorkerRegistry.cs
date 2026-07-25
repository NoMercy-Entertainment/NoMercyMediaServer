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

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Jobs;
using NoMercy.Storage;

namespace NoMercy.Encoder.Distribution;

/// <summary>
/// Persistent coordinator-side registry. Wraps <see cref="InMemoryRemoteWorkerRegistry"/>
/// for all runtime behaviour (heartbeats, cooldown, stale eviction) and layers
/// JSON persistence on top for the three durable operations:
///
///   Register  → write workers.json (atomic temp-file + move)
///   Unregister → write updated workers.json
///   Startup   → hydrate from workers.json if it exists
///
/// Workers re-register via <see cref="WorkerSelfRegistrationService"/> on
/// boot, so heartbeat freshness resets naturally. Entries that survive a
/// coordinator restart come back as stale (LastSeen = epoch), but the
/// registry's normal eviction path drops them from GetActiveWorkers().
/// Workers that are offline simply don't heartbeat and get pruned; workers
/// that come back online re-register and get restored.
///
/// This class does NOT implement <see cref="InMemoryRemoteWorkerRegistry"/>
/// extension points (RecordTaskOutcome / Heartbeat / GetAllWorkersWithHealth)
/// directly — it delegates every call to an inner instance so callers
/// that cast to the concrete type keep working.
/// </summary>
public class JsonRemoteWorkerRegistry : IRemoteWorkerRegistry
{
    private readonly InMemoryRemoteWorkerRegistry _inner;
    private readonly string _filePath;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITaskSerializer _serializer;
    private readonly byte[] _signingKey;
    private readonly ILogger<JsonRemoteWorkerRegistry> _logger;
    private readonly IStorage _storage;

    // Keep a parallel dictionary of the durable worker descriptors so we
    // can reconstruct HttpRemoteWorker instances on rehydration without
    // needing the full DI graph.
    private readonly ConcurrentDictionary<string, PersistedWorkerEntry> _persisted = new();

    public JsonRemoteWorkerRegistry(
        InMemoryRemoteWorkerRegistry inner,
        string filePath,
        IHttpClientFactory httpClientFactory,
        ITaskSerializer serializer,
        byte[] signingKey,
        ILogger<JsonRemoteWorkerRegistry> logger,
        IStorage storage
    )
    {
        _inner = inner;
        _filePath = filePath;
        _httpClientFactory = httpClientFactory;
        _serializer = serializer;
        _signingKey = signingKey;
        _logger = logger;
        _storage = storage;

        HydrateFromDisk();
    }

    // ── IRemoteWorkerRegistry ──────────────────────────────────────────────

    public IReadOnlyList<IRemoteWorker> GetActiveWorkers() => _inner.GetActiveWorkers();

    // ── Pass-through methods matching InMemoryRemoteWorkerRegistry API ────

    public void Register(IRemoteWorker worker)
    {
        _inner.Register(worker);

        if (worker is HttpRemoteWorker http)
        {
            ResourceBudgetSnapshot budget = http.GetAvailableBudget();
            IHardwareCapabilities caps = http.GetCapabilities();
            _persisted[worker.WorkerId] = new(
                WorkerId: worker.WorkerId,
                BaseUrl: http.BaseUrl,
                CpuCores: caps.CpuCores,
                AvailableCpuThreads: budget.AvailableCpuThreads,
                AvailableGpuSlots: budget.AvailableGpuSlots,
                Gpus: caps.Gpus.ToList()
            );
            FlushToDisk();
        }
    }

    public bool Unregister(string workerId)
    {
        bool removed = _inner.Unregister(workerId);
        if (removed)
        {
            _persisted.TryRemove(workerId, out _);
            FlushToDisk();
        }

        return removed;
    }

    public bool Heartbeat(string workerId) => _inner.Heartbeat(workerId);

    public void RecordTaskOutcome(string workerId, bool success) =>
        _inner.RecordTaskOutcome(workerId, success);

    public IReadOnlyList<WorkerHealthSnapshot> GetAllWorkersWithHealth() =>
        _inner.GetAllWorkersWithHealth();

    // ── Persistence ────────────────────────────────────────────────────────

    private void HydrateFromDisk()
    {
        if (!_storage.Exists(_filePath))
            return;

        try
        {
            string json = Encoding.UTF8.GetString(_storage.Read(_filePath));
            List<PersistedWorkerEntry>? entries = JsonSerializer.Deserialize<
                List<PersistedWorkerEntry>
            >(json, JsonOptions);

            if (entries is null || entries.Count == 0)
                return;

            _logger.LogInformation(
                "Rehydrating {Count} worker(s) from {Path}",
                entries.Count,
                _filePath
            );

            foreach (PersistedWorkerEntry entry in entries)
            {
                if (
                    string.IsNullOrWhiteSpace(entry.WorkerId)
                    || string.IsNullOrWhiteSpace(entry.BaseUrl)
                )
                    continue;

                if (!Uri.TryCreate(entry.BaseUrl, UriKind.Absolute, out Uri? baseUri))
                    continue;

                // An HttpRemoteWorker must address an http(s) endpoint. .NET on
                // Unix parses a rooted path like "/foo" into an absolute file:// URI,
                // so guard the scheme explicitly to drop non-network entries
                // consistently across platforms.
                if (baseUri.Scheme != Uri.UriSchemeHttp
                    && baseUri.Scheme != Uri.UriSchemeHttps)
                    continue;

                HttpClient http = _httpClientFactory.CreateClient("remote-worker");
                http.BaseAddress = baseUri;
                http.Timeout = TimeSpan.FromMinutes(10);

                HttpRemoteWorker worker = new(
                    workerId: entry.WorkerId,
                    http: http,
                    serializer: _serializer,
                    signingKey: _signingKey,
                    initialCapabilities: new HardwareCapabilities(
                        Gpus: entry.Gpus ?? [],
                        CpuCores: entry.CpuCores
                    ),
                    initialBudget: new(
                        AvailableGpuSlots: entry.AvailableGpuSlots,
                        AvailableCpuThreads: entry.AvailableCpuThreads,
                        GpuUtilization: 0
                    ),
                    logger: new NullLogger<HttpRemoteWorker>()
                );

                _inner.Register(worker);
                _persisted[entry.WorkerId] = entry;

                _logger.LogDebug(
                    "Rehydrated worker {WorkerId} at {BaseUrl}",
                    entry.WorkerId,
                    entry.BaseUrl
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to rehydrate workers from {Path} — starting with empty registry",
                _filePath
            );
        }
    }

    private void FlushToDisk()
    {
        try
        {
            string dir = Path.GetDirectoryName(_filePath)!;
            _storage.CreateDirectory(dir);

            List<PersistedWorkerEntry> entries = _persisted.Values.ToList();
            string json = JsonSerializer.Serialize(entries, JsonOptions);

            // Atomic write: temp file in same directory + Move overwrites.
            string tmp = _filePath + ".tmp";
            _storage.Write(tmp, Encoding.UTF8.GetBytes(json));
            if (_storage.Exists(_filePath))
                _storage.Delete(_filePath);
            _storage.Move(tmp, _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist workers to {Path}", _filePath);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

/// <summary>
/// Durable subset of worker state. Heartbeat freshness and health counters
/// are transient — they reset when the coordinator restarts; workers
/// re-register and re-heartbeat to restore their active status.
/// </summary>
internal sealed record PersistedWorkerEntry(
    string WorkerId,
    string BaseUrl,
    int CpuCores,
    int AvailableCpuThreads,
    int AvailableGpuSlots,
    List<GpuDevice>? Gpus = null
);
