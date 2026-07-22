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
/// Persistence tests for <see cref="JsonRemoteWorkerRegistry"/>.
/// Each test gets its own temp file so they run safely in parallel.
/// </summary>
public class JsonRemoteWorkerRegistryTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;
    private readonly byte[] _signingKey = Encoding.UTF8.GetBytes(
        s: "test-signing-key-32-bytes-padded"
    );
    private readonly TaskSerializer _serializer = new();

    public JsonRemoteWorkerRegistryTests()
    {
        _dir = Path.Combine(path1: Path.GetTempPath(), path2: "nm-registry-tests", path3: Guid.NewGuid().ToString(format: "N"));
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
        // Returns an HttpClient with a stub handler so rehydration doesn't
        // attempt real network calls.
        Mock<IHttpClientFactory> factory = new();
        factory
            .Setup(expression: f => f.CreateClient(It.IsAny<string>()))
            .Returns(valueFunction: () =>
                new(handler: new NoOpHandler()) { BaseAddress = new(uriString: "http://worker.test/") }
            );
        return factory.Object;
    }

    private HttpRemoteWorker MakeWorker(string id, string baseUrl = "http://worker.test/") =>
        new(
            workerId: id,
            http: new(handler: new NoOpHandler()) { BaseAddress = new(uriString: baseUrl) },
            serializer: _serializer,
            signingKey: _signingKey,
            initialCapabilities: new HardwareCapabilities(Gpus: [], CpuCores: 4),
            initialBudget: new(AvailableGpuSlots: 0, AvailableCpuThreads: 4, GpuUtilization: 0),
            logger: NullLogger<HttpRemoteWorker>.Instance
        );

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public void Register_HttpRemoteWorker_WritesJsonFile()
    {
        JsonRemoteWorkerRegistry sut = BuildRegistry();

        sut.Register(worker: MakeWorker(id: "beast", baseUrl: "http://beast.local/"));

        File.Exists(path: _path).Should().BeTrue(because: "registration must persist workers.json");
    }

    [Fact]
    public void RegisterAndReload_RestoresWorkers()
    {
        // First instance: register a worker, then dispose (go away).
        JsonRemoteWorkerRegistry first = BuildRegistry();
        first.Register(worker: MakeWorker(id: "phoenix", baseUrl: "http://phoenix.local/"));

        // Second instance: fresh object from the same path.
        JsonRemoteWorkerRegistry second = BuildRegistry();

        IReadOnlyList<IRemoteWorker> active = second.GetActiveWorkers();

        active.Should().HaveCount(expected: 1, because: "persisted worker must rehydrate on construction");
        active[index: 0].WorkerId.Should().Be(expected: "phoenix");
    }

    [Fact]
    public void RegisterMultiple_AllPersisted_AllRehydrated()
    {
        JsonRemoteWorkerRegistry first = BuildRegistry();
        first.Register(worker: MakeWorker(id: "w1", baseUrl: "http://w1.local/"));
        first.Register(worker: MakeWorker(id: "w2", baseUrl: "http://w2.local/"));
        first.Register(worker: MakeWorker(id: "w3", baseUrl: "http://w3.local/"));

        JsonRemoteWorkerRegistry second = BuildRegistry();

        second.GetActiveWorkers().Should().HaveCount(expected: 3, because: "all three workers must survive a restart");
    }

    [Fact]
    public void Unregister_RemovesFromFile_NotRehydratedAfterRestart()
    {
        JsonRemoteWorkerRegistry first = BuildRegistry();
        first.Register(worker: MakeWorker(id: "removable", baseUrl: "http://removable.local/"));
        first.Unregister(workerId: "removable");

        JsonRemoteWorkerRegistry second = BuildRegistry();

        second.GetActiveWorkers().Should().BeEmpty(because: "unregistered worker must not rehydrate");
    }

    [Fact]
    public void ReRegister_SameId_OverwritesEntry()
    {
        JsonRemoteWorkerRegistry first = BuildRegistry();
        first.Register(worker: MakeWorker(id: "stable", baseUrl: "http://stable.local/"));
        first.Register(worker: MakeWorker(id: "stable", baseUrl: "http://stable.local/")); // re-register

        JsonRemoteWorkerRegistry second = BuildRegistry();

        second
            .GetActiveWorkers()
            .Should()
            .HaveCount(expected: 1, because: "re-register must not create duplicate persistence entry");
    }

    [Fact]
    public void MissingFile_StartsEmpty_NoException()
    {
        // Path points at a file that doesn't exist.
        JsonRemoteWorkerRegistry sut = new(
            inner: new(),
            filePath: Path.Combine(path1: _dir, path2: "nonexistent.json"),
            httpClientFactory: MakeHttpClientFactory(),
            serializer: _serializer,
            signingKey: _signingKey,
            logger: NullLogger<JsonRemoteWorkerRegistry>.Instance,
            storage: MakeStorage()
        );

        sut.GetActiveWorkers().Should().BeEmpty();
    }

    [Fact]
    public void CorruptFile_StartsEmpty_NoException()
    {
        File.WriteAllText(path: _path, contents: "{ this is not valid json !!!}");

        JsonRemoteWorkerRegistry sut = BuildRegistry();

        sut.GetActiveWorkers().Should().BeEmpty(because: "corrupt file must be handled gracefully");
    }

    [Fact]
    public void Heartbeat_DelegatesInner_DoesNotAffectPersistence()
    {
        JsonRemoteWorkerRegistry sut = BuildRegistry();
        sut.Register(worker: MakeWorker(id: "hb", baseUrl: "http://hb.local/"));

        // Heartbeat should return true (worker is registered in inner).
        sut.Heartbeat(workerId: "hb").Should().BeTrue();
    }

    [Fact]
    public void RecordTaskOutcome_DelegatesInner()
    {
        JsonRemoteWorkerRegistry sut = BuildRegistry();
        sut.Register(worker: MakeWorker(id: "tracked", baseUrl: "http://tracked.local/"));

        // Should not throw — delegates to inner InMemoryRemoteWorkerRegistry.
        Action act = () =>
        {
            sut.RecordTaskOutcome(workerId: "tracked", success: false);
            sut.RecordTaskOutcome(workerId: "tracked", success: false);
            sut.RecordTaskOutcome(workerId: "tracked", success: false);
        };
        act.Should().NotThrow();

        // After 3 failures the inner registry puts the worker in cooldown.
        sut.GetActiveWorkers()
            .Should()
            .BeEmpty(because: "3 failures must trigger cooldown via inner registry");
    }

    [Fact]
    public void GetAllWorkersWithHealth_DelegatesInner()
    {
        JsonRemoteWorkerRegistry sut = BuildRegistry();
        sut.Register(worker: MakeWorker(id: "healthy", baseUrl: "http://healthy.local/"));
        sut.Register(worker: MakeWorker(id: "cooling", baseUrl: "http://cooling.local/"));
        sut.RecordTaskOutcome(workerId: "cooling", success: false);
        sut.RecordTaskOutcome(workerId: "cooling", success: false);
        sut.RecordTaskOutcome(workerId: "cooling", success: false);

        IReadOnlyList<WorkerHealthSnapshot> snapshots = sut.GetAllWorkersWithHealth();

        snapshots.Should().HaveCount(expected: 2, because: "health snapshot must include cooled-down workers");
    }

    // ── Stub HTTP handler ─────────────────────────────────────────────────

    private sealed class NoOpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(result: new HttpResponseMessage(statusCode: HttpStatusCode.OK));
    }
}
