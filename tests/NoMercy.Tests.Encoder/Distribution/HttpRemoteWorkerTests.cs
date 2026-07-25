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
using NoMercy.Encoder.Distribution;
using NoMercy.Encoder.Hardware;

namespace NoMercy.Tests.Encoder.Distribution;

public class HttpRemoteWorkerTests
{
    private readonly byte[] _signingKey = Encoding.UTF8.GetBytes(
        "http-worker-signing-key-32bytes!"
    );
    private readonly TaskSerializer _serializer = new();

    [Fact]
    public async Task ExecuteTaskAsync_SuccessResponse_ReturnsSignedResult()
    {
        DispatchResult workerResult = new(
            "t1",
            true,
            "/remote/out/t1",
            TimeSpan.FromSeconds(7)
        );
        string signedResponse = _serializer.SerializeResult(workerResult, _signingKey);

        HttpClient http = MakeClientReturning(HttpStatusCode.OK, signedResponse);
        HttpRemoteWorker sut = MakeWorker("beast", http);

        EncodeTask task = MakeTask("t1");
        DispatchResult result = await sut.ExecuteTaskAsync(task, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.OutputPath.Should().Be("/remote/out/t1");
        result.WorkerId.Should().Be("beast", "dispatcher always stamps the worker id");
    }

    [Fact]
    public async Task ExecuteTaskAsync_WorkerReturns500_ReturnsFailureDispatchResult()
    {
        HttpClient http = MakeClientReturning(
            HttpStatusCode.InternalServerError,
            "worker exploded"
        );
        HttpRemoteWorker sut = MakeWorker("broken", http);

        DispatchResult result = await sut.ExecuteTaskAsync(MakeTask("t2"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("500");
        result.WorkerId.Should().Be("broken");
    }

    [Fact]
    public async Task ExecuteTaskAsync_HttpRequestException_ReturnsFailureDispatchResult()
    {
        HttpClient http = MakeClientThrowing(new HttpRequestException("connection refused"));
        HttpRemoteWorker sut = MakeWorker("offline", http);

        DispatchResult result = await sut.ExecuteTaskAsync(MakeTask("t3"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("connection refused");
        result.WorkerId.Should().Be("offline");
    }

    [Fact]
    public async Task ExecuteTaskAsync_UnsignedResponse_ReturnsVerificationFailure()
    {
        // Worker returns 200 but with a body that doesn't pass HMAC —
        // treated as tampered / compromised worker, refuse the result.
        HttpClient http = MakeClientReturning(HttpStatusCode.OK, "{\"Success\":true}");
        HttpRemoteWorker sut = MakeWorker("evil", http);

        DispatchResult result = await sut.ExecuteTaskAsync(MakeTask("t4"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("HMAC");
    }

    [Fact]
    public async Task ExecuteTaskAsync_Cancellation_Throws()
    {
        HttpClient http = MakeClientReturning(HttpStatusCode.OK, "ignored");
        HttpRemoteWorker sut = MakeWorker("w", http);

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        Func<Task> act = () => sut.ExecuteTaskAsync(MakeTask("t5"), cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void UpdateSnapshot_RefreshesBudget()
    {
        HttpClient http = new();
        HttpRemoteWorker sut = MakeWorker("w", http);

        sut.GetAvailableBudget().AvailableCpuThreads.Should().Be(4);

        sut.UpdateSnapshot(
            MakeCapabilities(),
            new(2, 16, 0.1)
        );

        sut.GetAvailableBudget().AvailableCpuThreads.Should().Be(16);
        sut.GetAvailableBudget().AvailableGpuSlots.Should().Be(2);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private HttpRemoteWorker MakeWorker(string id, HttpClient http) =>
        new(
            id,
            http,
            _serializer,
            _signingKey,
            MakeCapabilities(),
            new(0, 4, 0),
            NullLogger<HttpRemoteWorker>.Instance
        );

    private static IHardwareCapabilities MakeCapabilities() => new HardwareCapabilities([], 4);

    private static EncodeTask MakeTask(string id) =>
        new(
            id,
            new("ffmpeg", [], null),
            $"/out/{id}",
            EncodeTaskType.QualityVariant
        );

    private static HttpClient MakeClientReturning(HttpStatusCode status, string body) =>
        new(
            new FakeHandler(
                (req, ct) =>
                    Task.FromResult(
                        new HttpResponseMessage(status) { Content = new StringContent(body) }
                    )
            )
        )
        {
            BaseAddress = new("http://worker.test/"),
        };

    private static HttpClient MakeClientThrowing(Exception ex) =>
        new(new FakeHandler((req, ct) => Task.FromException<HttpResponseMessage>(ex)))
        {
            BaseAddress = new("http://worker.test/"),
        };

    private sealed class FakeHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond
    ) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return respond(request, cancellationToken);
        }
    }
}
