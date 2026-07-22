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
        s: "http-worker-signing-key-32bytes!"
    );
    private readonly TaskSerializer _serializer = new();

    [Fact]
    public async Task ExecuteTaskAsync_SuccessResponse_ReturnsSignedResult()
    {
        DispatchResult workerResult = new(
            TaskId: "t1",
            Success: true,
            OutputPath: "/remote/out/t1",
            Duration: TimeSpan.FromSeconds(seconds: 7)
        );
        string signedResponse = _serializer.SerializeResult(result: workerResult, signingKey: _signingKey);

        HttpClient http = MakeClientReturning(status: HttpStatusCode.OK, body: signedResponse);
        HttpRemoteWorker sut = MakeWorker(id: "beast", http: http);

        EncodeTask task = MakeTask(id: "t1");
        DispatchResult result = await sut.ExecuteTaskAsync(task: task, ct: CancellationToken.None);

        result.Success.Should().BeTrue();
        result.OutputPath.Should().Be(expected: "/remote/out/t1");
        result.WorkerId.Should().Be(expected: "beast", because: "dispatcher always stamps the worker id");
    }

    [Fact]
    public async Task ExecuteTaskAsync_WorkerReturns500_ReturnsFailureDispatchResult()
    {
        HttpClient http = MakeClientReturning(
            status: HttpStatusCode.InternalServerError,
            body: "worker exploded"
        );
        HttpRemoteWorker sut = MakeWorker(id: "broken", http: http);

        DispatchResult result = await sut.ExecuteTaskAsync(task: MakeTask(id: "t2"), ct: CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain(expected: "500");
        result.WorkerId.Should().Be(expected: "broken");
    }

    [Fact]
    public async Task ExecuteTaskAsync_HttpRequestException_ReturnsFailureDispatchResult()
    {
        HttpClient http = MakeClientThrowing(ex: new HttpRequestException(message: "connection refused"));
        HttpRemoteWorker sut = MakeWorker(id: "offline", http: http);

        DispatchResult result = await sut.ExecuteTaskAsync(task: MakeTask(id: "t3"), ct: CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain(expected: "connection refused");
        result.WorkerId.Should().Be(expected: "offline");
    }

    [Fact]
    public async Task ExecuteTaskAsync_UnsignedResponse_ReturnsVerificationFailure()
    {
        // Worker returns 200 but with a body that doesn't pass HMAC —
        // treated as tampered / compromised worker, refuse the result.
        HttpClient http = MakeClientReturning(status: HttpStatusCode.OK, body: "{\"Success\":true}");
        HttpRemoteWorker sut = MakeWorker(id: "evil", http: http);

        DispatchResult result = await sut.ExecuteTaskAsync(task: MakeTask(id: "t4"), ct: CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain(expected: "HMAC");
    }

    [Fact]
    public async Task ExecuteTaskAsync_Cancellation_Throws()
    {
        HttpClient http = MakeClientReturning(status: HttpStatusCode.OK, body: "ignored");
        HttpRemoteWorker sut = MakeWorker(id: "w", http: http);

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        Func<Task> act = () => sut.ExecuteTaskAsync(task: MakeTask(id: "t5"), ct: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void UpdateSnapshot_RefreshesBudget()
    {
        HttpClient http = new();
        HttpRemoteWorker sut = MakeWorker(id: "w", http: http);

        sut.GetAvailableBudget().AvailableCpuThreads.Should().Be(expected: 4);

        sut.UpdateSnapshot(
            capabilities: MakeCapabilities(),
            budget: new(AvailableGpuSlots: 2, AvailableCpuThreads: 16, GpuUtilization: 0.1)
        );

        sut.GetAvailableBudget().AvailableCpuThreads.Should().Be(expected: 16);
        sut.GetAvailableBudget().AvailableGpuSlots.Should().Be(expected: 2);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private HttpRemoteWorker MakeWorker(string id, HttpClient http) =>
        new(
            workerId: id,
            http: http,
            serializer: _serializer,
            signingKey: _signingKey,
            initialCapabilities: MakeCapabilities(),
            initialBudget: new(AvailableGpuSlots: 0, AvailableCpuThreads: 4, GpuUtilization: 0),
            logger: NullLogger<HttpRemoteWorker>.Instance
        );

    private static IHardwareCapabilities MakeCapabilities() => new HardwareCapabilities(Gpus: [], CpuCores: 4);

    private static EncodeTask MakeTask(string id) =>
        new(
            TaskId: id,
            Command: new(Executable: "ffmpeg", Arguments: [], WorkingDirectory: null),
            OutputPath: $"/out/{id}",
            Type: EncodeTaskType.QualityVariant
        );

    private static HttpClient MakeClientReturning(HttpStatusCode status, string body) =>
        new(
            handler: new FakeHandler(
                respond: (req, ct) =>
                    Task.FromResult(
                        result: new HttpResponseMessage(statusCode: status) { Content = new StringContent(content: body) }
                    )
            )
        )
        {
            BaseAddress = new(uriString: "http://worker.test/"),
        };

    private static HttpClient MakeClientThrowing(Exception ex) =>
        new(handler: new FakeHandler(respond: (req, ct) => Task.FromException<HttpResponseMessage>(exception: ex)))
        {
            BaseAddress = new(uriString: "http://worker.test/"),
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
            return respond(arg1: request, arg2: cancellationToken);
        }
    }
}
