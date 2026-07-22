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

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Distribution;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Jobs;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Encoder.Distribution;

/// <summary>
/// Branch-coverage gaps for <see cref="JsonRemoteWorkerRegistry"/> beyond the
/// happy-path / corrupt-file / multi-worker cases already covered:
///
/// • Hydration entry-level filtering — entries with empty WorkerId or BaseUrl
///   must be silently skipped (not rehydrated, not removed from the file).
/// • Hydration with invalid Uri in BaseUrl — must not crash; entry skipped.
/// • Empty entries list — early-return path, no logging or registration.
/// • Unregister of unknown worker — returns false, no flush, file untouched.
/// • Non-HttpRemoteWorker registration — pure in-memory, no persistence
///   (only HttpRemoteWorker entries land in workers.json).
/// </summary>
public class JsonRemoteWorkerRegistryBranchTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;
    private readonly byte[] _signingKey = Encoding.UTF8.GetBytes(
        s: "test-signing-key-32-bytes-padded"
    );
    private readonly TaskSerializer _serializer = new();

    public JsonRemoteWorkerRegistryBranchTests()
    {
        _dir = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "nm-registry-branch-tests",
            path3: Guid.NewGuid().ToString(format: "N")
        );
        Directory.CreateDirectory(path: _dir);
        _path = Path.Combine(path1: _dir, path2: "workers.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(path: _dir, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
        GC.SuppressFinalize(obj: this);
    }

    // ── Hydration: empty WorkerId / BaseUrl skipped ─────────────────────────

    [Fact]
    public void Entry_with_empty_WorkerId_is_skipped_during_hydration()
    {
        // A corrupted persistence file could end up with empty WorkerId after
        // a partial write. Rehydration must skip such entries silently.
        WritePersistedEntries(entries:
            [
                new
                {
                    WorkerId = "",
                    BaseUrl = "http://valid.local/",
                    CpuCores = 4,
                    AvailableCpuThreads = 4,
                    AvailableGpuSlots = 0,
                },
                new
                {
                    WorkerId = "valid-worker",
                    BaseUrl = "http://valid.local/",
                    CpuCores = 4,
                    AvailableCpuThreads = 4,
                    AvailableGpuSlots = 0,
                }
            ]
        );

        JsonRemoteWorkerRegistry sut = BuildRegistry();

        IReadOnlyList<IRemoteWorker> active = sut.GetActiveWorkers();
        active.Should().HaveCount(expected: 1);
        active[index: 0].WorkerId.Should().Be(expected: "valid-worker");
    }

    [Fact]
    public void Entry_with_empty_BaseUrl_is_skipped_during_hydration()
    {
        WritePersistedEntries(
            entries: new
            {
                WorkerId = "no-url-worker",
                BaseUrl = "",
                CpuCores = 4,
                AvailableCpuThreads = 4,
                AvailableGpuSlots = 0,
            }
        );

        JsonRemoteWorkerRegistry sut = BuildRegistry();
        sut.GetActiveWorkers().Should().BeEmpty();
    }

    [Fact]
    public void Entry_with_whitespace_BaseUrl_is_skipped_during_hydration()
    {
        WritePersistedEntries(
            entries: new
            {
                WorkerId = "ws-url-worker",
                BaseUrl = "   ",
                CpuCores = 4,
                AvailableCpuThreads = 4,
                AvailableGpuSlots = 0,
            }
        );

        JsonRemoteWorkerRegistry sut = BuildRegistry();
        sut.GetActiveWorkers().Should().BeEmpty();
    }

    // ── Hydration: invalid Uri skipped ──────────────────────────────────────

    [Fact]
    public void Entry_with_invalid_Uri_is_skipped_during_hydration()
    {
        // BaseUrl is non-empty but not parseable as an absolute Uri —
        // Uri.TryCreate returns false and the entry is dropped.
        WritePersistedEntries(entries:
            [
                new
                {
                    WorkerId = "bad-uri",
                    BaseUrl = "not a uri at all",
                    CpuCores = 4,
                    AvailableCpuThreads = 4,
                    AvailableGpuSlots = 0,
                },
                new
                {
                    WorkerId = "good-uri",
                    BaseUrl = "http://good.local/",
                    CpuCores = 4,
                    AvailableCpuThreads = 4,
                    AvailableGpuSlots = 0,
                }
            ]
        );

        JsonRemoteWorkerRegistry sut = BuildRegistry();
        IReadOnlyList<IRemoteWorker> active = sut.GetActiveWorkers();

        active.Should().HaveCount(expected: 1);
        active[index: 0].WorkerId.Should().Be(expected: "good-uri");
    }

    [Fact]
    public void Entry_with_relative_Uri_is_skipped_during_hydration()
    {
        // Uri.TryCreate with UriKind.Absolute rejects relative URIs.
        WritePersistedEntries(
            entries: new
            {
                WorkerId = "relative",
                BaseUrl = "/relative/path",
                CpuCores = 4,
                AvailableCpuThreads = 4,
                AvailableGpuSlots = 0,
            }
        );

        JsonRemoteWorkerRegistry sut = BuildRegistry();
        sut.GetActiveWorkers().Should().BeEmpty();
    }

    // ── Hydration: empty list early-return ──────────────────────────────────

    [Fact]
    public void Empty_persisted_list_returns_clean_without_loading_inner_workers()
    {
        // File exists but contains an empty array — HydrateFromDisk should
        // hit the early-return branch (entries.Count == 0) without iterating.
        File.WriteAllText(path: _path, contents: "[]");

        JsonRemoteWorkerRegistry sut = BuildRegistry();
        sut.GetActiveWorkers().Should().BeEmpty();
    }

    // ── Unregister of unknown worker ────────────────────────────────────────

    [Fact]
    public void Unregister_unknown_worker_returns_false_and_does_not_flush()
    {
        JsonRemoteWorkerRegistry sut = BuildRegistry();

        // Register one so the file is created with the known entry.
        sut.Register(worker: MakeWorker(id: "alpha", baseUrl: "http://alpha.local/"));
        File.Exists(path: _path).Should().BeTrue();
        long sizeAfterRegister = new FileInfo(fileName: _path).Length;

        // Unregister a workerId that doesn't exist.
        bool removed = sut.Unregister(workerId: "unknown-worker");

        removed.Should().BeFalse();
        // File still exists and still contains the original entry — no
        // mistaken flush that would erase the live worker.
        File.Exists(path: _path).Should().BeTrue();
        new FileInfo(fileName: _path).Length.Should().Be(expected: sizeAfterRegister);
    }

    // ── Non-HttpRemoteWorker registration ───────────────────────────────────

    [Fact]
    public void Register_non_HttpRemoteWorker_does_not_persist_to_disk()
    {
        // The persistence path only fires for HttpRemoteWorker (it has the
        // BaseUrl + capabilities we need to rehydrate). Other IRemoteWorker
        // implementations are pure in-memory and intentionally NOT persisted.
        JsonRemoteWorkerRegistry sut = BuildRegistry();

        // Inject a stub IRemoteWorker that isn't an HttpRemoteWorker.
        StubRemoteWorker stub = new(workerId: "local-only");
        sut.Register(worker: stub);

        // Inner registry should know about it.
        sut.GetActiveWorkers().Should().Contain(predicate: w => w.WorkerId == "local-only");

        // But the file must NOT have been touched.
        File.Exists(path: _path).Should().BeFalse(because: "non-Http workers must not persist");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IStorage MakeStorage() =>
        new LocalStorage(
            driver: new LocalStorageDriver(),
            guard: new(allowedRoots: [], driver: new LocalStorageDriver())
        );

    private JsonRemoteWorkerRegistry BuildRegistry() =>
        new(
            inner: new(),
            filePath: _path,
            httpClientFactory: MakeHttpClientFactory(),
            serializer: _serializer,
            signingKey: _signingKey,
            logger: NullLogger<JsonRemoteWorkerRegistry>.Instance,
            storage: MakeStorage()
        );

    private static IHttpClientFactory MakeHttpClientFactory()
    {
        Mock<IHttpClientFactory> factory = new();
        factory
            .Setup(expression: f => f.CreateClient(It.IsAny<string>()))
            .Returns(valueFunction: () =>
                new(handler: new NoOpHandler()) { BaseAddress = new(uriString: "http://worker.test/") }
            );
        return factory.Object;
    }

    private HttpRemoteWorker MakeWorker(string id, string baseUrl) =>
        new(
            workerId: id,
            http: new(handler: new NoOpHandler()) { BaseAddress = new(uriString: baseUrl) },
            serializer: _serializer,
            signingKey: _signingKey,
            initialCapabilities: new HardwareCapabilities(Gpus: [], CpuCores: 4),
            initialBudget: new(AvailableGpuSlots: 0, AvailableCpuThreads: 4, GpuUtilization: 0),
            logger: NullLogger<HttpRemoteWorker>.Instance
        );

    private void WritePersistedEntries(params object[] entries)
    {
        // Mirror JsonRemoteWorkerRegistry.JsonOptions (snake_case, indent on).
        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };
        File.WriteAllText(path: _path, contents: JsonSerializer.Serialize(value: entries, options: options));
    }

    private sealed class NoOpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(result: new HttpResponseMessage(statusCode: HttpStatusCode.OK));
    }

    private sealed class StubRemoteWorker(string workerId) : IRemoteWorker
    {
        public string WorkerId { get; } = workerId;

        public ResourceBudgetSnapshot GetAvailableBudget() =>
            new(AvailableGpuSlots: 0, AvailableCpuThreads: 1, GpuUtilization: 0);

        public IHardwareCapabilities GetCapabilities() => new HardwareCapabilities(Gpus: [], CpuCores: 1);

        public Task<RemoteEncodingResult> ExecuteJobAsync(
            EncodingJob job,
            IProgress<NoMercy.Encoder.Progress.EncodingProgress> progress,
            CancellationToken ct
        ) => throw new NotImplementedException(message: "Stub for branch test");

        public Task<DispatchResult> ExecuteTaskAsync(EncodeTask task, CancellationToken ct) =>
            Task.FromResult(result: new DispatchResult(TaskId: task.TaskId, Success: false, OutputPath: "", Duration: TimeSpan.Zero, Error: "stub"));
    }
}
