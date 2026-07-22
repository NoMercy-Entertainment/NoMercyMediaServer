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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Distribution;
using NoMercy.Encoder.Hardware;

namespace NoMercy.Tests.Encoder.Distribution;

public class WorkerSelfRegistrationServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WhenDisabled_ExitsImmediately()
    {
        // No CoordinatorUrl → service must no-op and exit cleanly without
        // hitting the network. Useful on standalone installs where the
        // hosted service is registered but shouldn't activate.
        RecordingHandler handler = new();
        WorkerSelfRegistrationService sut = MakeService(
            handler: handler,
            configure: opts =>
            {
                opts.DistributedEncodingSigningKey = "key";
                // CoordinatorUrl intentionally not set.
            }
        );

        using CancellationTokenSource cts = new(delay: TimeSpan.FromSeconds(seconds: 1));
        await sut.StartAsync(cancellationToken: cts.Token);
        await sut.StopAsync(cancellationToken: CancellationToken.None);

        handler.RequestLog.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_Enabled_PostsRegistrationOnStart()
    {
        TaskCompletionSource registerSeen = new(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingHandler handler = new(respond: req =>
        {
            if (req.RequestUri!.ToString().Contains(value: "register"))
                registerSeen.TrySetResult();
            return new(statusCode: HttpStatusCode.OK);
        });

        WorkerSelfRegistrationService sut = MakeService(
            handler: handler,
            configure: opts =>
            {
                opts.DistributedEncodingSigningKey = "key";
                opts.CoordinatorUrl = "http://coordinator.test";
                opts.WorkerSelfBaseUrl = "http://worker.test";
                opts.WorkerId = "test-worker";
                opts.WorkerHeartbeatInterval = TimeSpan.FromMilliseconds(milliseconds: 50);
            }
        );

        using CancellationTokenSource cts = new();
        await sut.StartAsync(cancellationToken: cts.Token);

        // Deterministic wait: signal fires when register is observed.
        await registerSeen.Task.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 5));

        await cts.CancelAsync();
        await sut.StopAsync(cancellationToken: CancellationToken.None);

        handler.RequestLog.Should().Contain(predicate: r => r.Path.Contains("register"));
    }

    [Fact]
    public async Task ExecuteAsync_Shutdown_SendsUnregisterDelete()
    {
        TaskCompletionSource registerSeen = new(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingHandler handler = new(respond: req =>
        {
            if (req.RequestUri!.ToString().Contains(value: "register") && req.Method == HttpMethod.Post)
                registerSeen.TrySetResult();
            return new(statusCode: HttpStatusCode.OK);
        });

        WorkerSelfRegistrationService sut = MakeService(
            handler: handler,
            configure: opts =>
            {
                opts.DistributedEncodingSigningKey = "key";
                opts.CoordinatorUrl = "http://coordinator.test";
                opts.WorkerSelfBaseUrl = "http://worker.test";
                opts.WorkerId = "tw";
                opts.WorkerHeartbeatInterval = TimeSpan.FromMilliseconds(milliseconds: 50);
            }
        );

        using CancellationTokenSource cts = new();
        await sut.StartAsync(cancellationToken: cts.Token);
        await registerSeen.Task.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 5));
        await sut.StopAsync(cancellationToken: CancellationToken.None);

        handler
            .RequestLog.Should()
            .Contain(predicate: r =>
                r.Method == HttpMethod.Delete && r.Path.Contains("/distribution/workers/tw")
            );
    }

    [Fact]
    public async Task ExecuteAsync_HeartbeatRejected_TriggersReregistration()
    {
        // Heartbeat returning 404 means "coordinator doesn't know us".
        // Service must retry register instead of staying in a 404 loop.
        int registerCount = 0;
        TaskCompletionSource secondRegister = new(
            creationOptions: TaskCreationOptions.RunContinuationsAsynchronously
        );
        RecordingHandler handler = new(respond: req =>
        {
            if (req.RequestUri!.ToString().Contains(value: "register"))
            {
                int n = Interlocked.Increment(location: ref registerCount);
                if (n >= 2)
                    secondRegister.TrySetResult();
                return new(statusCode: HttpStatusCode.OK);
            }
            if (req.RequestUri.ToString().Contains(value: "heartbeat"))
                return new(statusCode: HttpStatusCode.NotFound);
            return new(statusCode: HttpStatusCode.OK);
        });

        WorkerSelfRegistrationService sut = MakeService(
            handler: handler,
            configure: opts =>
            {
                opts.DistributedEncodingSigningKey = "key";
                opts.CoordinatorUrl = "http://coordinator.test";
                opts.WorkerSelfBaseUrl = "http://worker.test";
                opts.WorkerId = "tw";
                opts.WorkerHeartbeatInterval = TimeSpan.FromMilliseconds(milliseconds: 30);
            }
        );

        using CancellationTokenSource cts = new();
        await sut.StartAsync(cancellationToken: cts.Token);
        await secondRegister.Task.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 5));

        await cts.CancelAsync();
        await sut.StopAsync(cancellationToken: CancellationToken.None);

        Volatile
            .Read(location: ref registerCount)
            .Should()
            .BeGreaterThan(expected: 1, because: "heartbeat 404 must trigger re-registration");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static WorkerSelfRegistrationService MakeService(
        RecordingHandler handler,
        Action<EncoderOptions> configure
    )
    {
        EncoderOptions opts = new();
        configure(obj: opts);

        ServiceCollection services = new();
        services.AddSingleton(implementationInstance: opts);
        services.AddSingleton<IHardwareCapabilities>(implementationInstance: new HardwareCapabilities(Gpus: [], CpuCores: 4));
        services
            .AddHttpClient()
            .ConfigureHttpClientDefaults(configure: b => b.ConfigurePrimaryHttpMessageHandler(configureHandler: () => handler));

        ServiceProvider provider = services.BuildServiceProvider();

        return new(
            capabilities: provider.GetRequiredService<IHardwareCapabilities>(),
            options: opts,
            httpClientFactory: provider.GetRequiredService<IHttpClientFactory>(),
            logger: NullLogger<WorkerSelfRegistrationService>.Instance
        );
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        private readonly List<(HttpMethod Method, string Path)> _log = [];

        public RecordingHandler(HashSet<string>? okOn = null)
            : this(respond: req => new(statusCode: ShouldOk(req: req, okOn: okOn) ? HttpStatusCode.OK : HttpStatusCode.NotFound))
        { }

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        public IReadOnlyList<(HttpMethod Method, string Path)> RequestLog
        {
            get
            {
                lock (_log)
                    return _log.ToArray();
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_log)
                _log.Add(item: (request.Method, request.RequestUri!.AbsolutePath));
            return Task.FromResult(result: _respond(arg: request));
        }

        private static bool ShouldOk(HttpRequestMessage req, HashSet<string>? okOn)
        {
            if (okOn is null)
                return true;
            string path = req.RequestUri!.AbsolutePath;
            return okOn.Any(predicate: pattern => path.Contains(value: pattern));
        }
    }
}
