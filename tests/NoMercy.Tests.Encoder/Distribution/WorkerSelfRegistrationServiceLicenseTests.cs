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

/// <summary>
/// Tests for the license-gated token-rotation behaviour added in Phase 4.9.
///
/// These tests focus on:
///   1. Token near-expiry triggers a refresh call.
///   2. 403 from token endpoint marks license revoked and stops the loop.
///   3. Exponential backoff sequence is observed on repeated 401s.
/// </summary>
public class WorkerSelfRegistrationServiceLicenseTests
{
    // ── Token expiry triggers refresh ────────────────────────────────────────

    [Fact]
    public async Task Heartbeat_WhenTokenNearExpiry_RefreshesToken()
    {
        // Token expires in 30s — below the 60s lead time threshold.
        ClusterToken expiring = new(Secret: "old-secret", ExpiresAt: DateTime.UtcNow.AddSeconds(value: 30), Scopes: []);
        ClusterTokenHolder holder = new();
        holder.Update(token: expiring);

        // Fresh token that the fake client will return.
        ClusterToken fresh = new(Secret: "new-secret", ExpiresAt: DateTime.UtcNow.AddHours(value: 1), Scopes: []);
        TaskCompletionSource refreshCalled = new(
            creationOptions: TaskCreationOptions.RunContinuationsAsynchronously
        );

        FakeLicenseTokenClient licenseClient = new(onRequest: () =>
        {
            refreshCalled.TrySetResult();
            return new(Token: fresh, Failure: null, Message: null);
        });

        RecordingHandler http = new();
        WorkerSelfRegistrationService sut = MakeService(
            httpHandler: http,
            configure: opts =>
            {
                opts.DistributedEncodingSigningKey = "key";
                opts.CoordinatorUrl = "http://coordinator.test";
                opts.WorkerSelfBaseUrl = "http://worker.test";
                opts.WorkerId = "tw";
                opts.WorkerHeartbeatInterval = TimeSpan.FromMilliseconds(milliseconds: 30);
            },
            holder: holder,
            licenseClient: licenseClient
        );

        using CancellationTokenSource cts = new();
        await sut.StartAsync(cancellationToken: cts.Token);
        await refreshCalled.Task.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 5));
        await cts.CancelAsync();
        await sut.StopAsync(cancellationToken: CancellationToken.None);

        holder.Current!.Secret.Should().Be(expected: "new-secret");
    }

    // ── 403 triggers shutdown ────────────────────────────────────────────────

    [Fact]
    public async Task TokenRequest_403_SetsRevokedAndStopsLoop()
    {
        ClusterTokenHolder holder = new();
        FakeLicenseTokenClient licenseClient = new(onRequest: () =>
            new(Token: null, Failure: LicenseFailureKind.EntitlementRevoked, Message: "403 from coordinator")
        );

        TaskCompletionSource stopObserved = new(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingHandler http = new(respond: req =>
        {
            // We should never reach a heartbeat — service should stop.
            if (req.RequestUri!.ToString().Contains(value: "heartbeat"))
                stopObserved.TrySetResult();
            return new(statusCode: HttpStatusCode.OK);
        });

        WorkerSelfRegistrationService sut = MakeService(
            httpHandler: http,
            configure: opts =>
            {
                opts.DistributedEncodingSigningKey = "key";
                opts.CoordinatorUrl = "http://coordinator.test";
                opts.WorkerSelfBaseUrl = "http://worker.test";
                opts.WorkerId = "tw";
                opts.WorkerHeartbeatInterval = TimeSpan.FromMilliseconds(milliseconds: 30);
            },
            holder: holder,
            licenseClient: licenseClient
        );

        using CancellationTokenSource cts = new(delay: TimeSpan.FromSeconds(seconds: 3));
        await sut.StartAsync(cancellationToken: cts.Token);

        // Give service time to reach the 403 path and exit.
        await Task.Delay(millisecondsDelay: 200);
        await sut.StopAsync(cancellationToken: CancellationToken.None);

        holder.IsRevoked.Should().BeTrue();
        // Heartbeat must NOT have been reached — service exited before the loop.
        stopObserved
            .Task.IsCompleted.Should()
            .BeFalse(because: "heartbeat must never fire when license is revoked on initial token fetch");
    }

    // ── Backoff sequence on repeated 401s ────────────────────────────────────

    [Fact]
    public async Task TokenRequest_Repeated401_DelaysWithExponentialBackoff()
    {
        // This test verifies that consecutive 401 responses do not cause
        // an immediate tight loop.  We track call timestamps and assert that
        // the gap between calls grows.
        List<DateTime> callTimes = [];
        object callLock = new();
        int maxCalls = 4;
        TaskCompletionSource doneTcs = new(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);

        FakeLicenseTokenClient licenseClient = new(onRequest: () =>
        {
            DateTime now = DateTime.UtcNow;
            lock (callLock)
            {
                callTimes.Add(item: now);
                if (callTimes.Count >= maxCalls)
                    doneTcs.TrySetResult();
            }
            return new(Token: null, Failure: LicenseFailureKind.Unauthenticated, Message: "401");
        });

        ClusterTokenHolder holder = new();
        // Ensure the token is always "near expiry" so every heartbeat tick
        // attempts a refresh.
        holder.Update(token: new(Secret: "seed", ExpiresAt: DateTime.UtcNow.AddSeconds(value: 30), Scopes: []));

        RecordingHandler http = new();
        WorkerSelfRegistrationService sut = MakeService(
            httpHandler: http,
            configure: opts =>
            {
                opts.DistributedEncodingSigningKey = "key";
                opts.CoordinatorUrl = "http://coordinator.test";
                opts.WorkerSelfBaseUrl = "http://worker.test";
                opts.WorkerId = "tw";
                // Very short heartbeat so the test doesn't take forever.
                opts.WorkerHeartbeatInterval = TimeSpan.FromMilliseconds(milliseconds: 10);
            },
            holder: holder,
            licenseClient: licenseClient
        );

        using CancellationTokenSource cts = new();
        await sut.StartAsync(cancellationToken: cts.Token);
        await doneTcs.Task.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 15));
        await cts.CancelAsync();
        await sut.StopAsync(cancellationToken: CancellationToken.None);

        // The gaps between successive calls should be non-decreasing
        // (backoff grows).  We compare gap[1] > gap[0] as a minimal proof.
        callTimes.Count.Should().BeGreaterThanOrEqualTo(expected: 3);
        TimeSpan firstGap = callTimes[index: 1] - callTimes[index: 0];
        TimeSpan secondGap = callTimes[index: 2] - callTimes[index: 1];

        // The second gap should be at least as large as the first, because
        // the backoff delay doubles from 1s → 2s.
        secondGap
            .Should()
            .BeGreaterThanOrEqualTo(
                expected: firstGap,
                because: "backoff must not decrease between consecutive 401 failures"
            );
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WorkerSelfRegistrationService MakeService(
        RecordingHandler httpHandler,
        Action<EncoderOptions> configure,
        ClusterTokenHolder? holder = null,
        ILicenseTokenClient? licenseClient = null
    )
    {
        EncoderOptions opts = new();
        configure(obj: opts);

        ServiceCollection services = new();
        services.AddSingleton(implementationInstance: opts);
        services.AddSingleton<IHardwareCapabilities>(implementationInstance: new HardwareCapabilities(Gpus: [], CpuCores: 4));
        services
            .AddHttpClient()
            .ConfigureHttpClientDefaults(configure: b =>
                b.ConfigurePrimaryHttpMessageHandler(configureHandler: () => httpHandler)
            );

        ServiceProvider provider = services.BuildServiceProvider();

        return new(
            capabilities: provider.GetRequiredService<IHardwareCapabilities>(),
            options: opts,
            httpClientFactory: provider.GetRequiredService<IHttpClientFactory>(),
            logger: NullLogger<WorkerSelfRegistrationService>.Instance,
            tokenHolder: holder,
            licenseTokenClient: licenseClient
        );
    }

    // ── Fakes ─────────────────────────────────────────────────────────────────

    private sealed class FakeLicenseTokenClient(Func<ClusterTokenResult> onRequest)
        : ILicenseTokenClient
    {
        public Task<ClusterTokenResult> RequestAsync(CancellationToken ct) =>
            Task.FromResult(result: onRequest());

        public Task<IntrospectResult> IntrospectAsync(string token, CancellationToken ct) =>
            Task.FromResult(result: new IntrospectResult(Active: true, Scopes: [], Message: null));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage>? respond = null)
        {
            _respond = respond ?? (_ => new(statusCode: HttpStatusCode.OK));
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result: _respond(arg: request));
        }
    }
}
