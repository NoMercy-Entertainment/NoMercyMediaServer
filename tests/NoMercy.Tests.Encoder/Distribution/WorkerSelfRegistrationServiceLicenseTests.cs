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
        ClusterToken expiring = new("old-secret", DateTime.UtcNow.AddSeconds(30), []);
        ClusterTokenHolder holder = new();
        holder.Update(expiring);

        // Fresh token that the fake client will return.
        ClusterToken fresh = new("new-secret", DateTime.UtcNow.AddHours(1), []);
        TaskCompletionSource refreshCalled = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        FakeLicenseTokenClient licenseClient = new(onRequest: () =>
        {
            refreshCalled.TrySetResult();
            return new(fresh, null, null);
        });

        RecordingHandler http = new();
        WorkerSelfRegistrationService sut = MakeService(
            http,
            opts =>
            {
                opts.DistributedEncodingSigningKey = "key";
                opts.CoordinatorUrl = "http://coordinator.test";
                opts.WorkerSelfBaseUrl = "http://worker.test";
                opts.WorkerId = "tw";
                opts.WorkerHeartbeatInterval = TimeSpan.FromMilliseconds(30);
            },
            holder: holder,
            licenseClient: licenseClient
        );

        using CancellationTokenSource cts = new();
        await sut.StartAsync(cts.Token);
        await refreshCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cts.CancelAsync();
        await sut.StopAsync(CancellationToken.None);

        holder.Current!.Secret.Should().Be("new-secret");
    }

    // ── 403 triggers shutdown ────────────────────────────────────────────────

    [Fact]
    public async Task TokenRequest_403_SetsRevokedAndStopsLoop()
    {
        ClusterTokenHolder holder = new();
        FakeLicenseTokenClient licenseClient = new(onRequest: () =>
            new(null, LicenseFailureKind.EntitlementRevoked, "403 from coordinator")
        );

        TaskCompletionSource stopObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingHandler http = new(respond: req =>
        {
            // We should never reach a heartbeat — service should stop.
            if (req.RequestUri!.ToString().Contains("heartbeat"))
                stopObserved.TrySetResult();
            return new(HttpStatusCode.OK);
        });

        WorkerSelfRegistrationService sut = MakeService(
            http,
            opts =>
            {
                opts.DistributedEncodingSigningKey = "key";
                opts.CoordinatorUrl = "http://coordinator.test";
                opts.WorkerSelfBaseUrl = "http://worker.test";
                opts.WorkerId = "tw";
                opts.WorkerHeartbeatInterval = TimeSpan.FromMilliseconds(30);
            },
            holder: holder,
            licenseClient: licenseClient
        );

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(3));
        await sut.StartAsync(cts.Token);

        // Give service time to reach the 403 path and exit.
        await Task.Delay(200);
        await sut.StopAsync(CancellationToken.None);

        holder.IsRevoked.Should().BeTrue();
        // Heartbeat must NOT have been reached — service exited before the loop.
        stopObserved
            .Task.IsCompleted.Should()
            .BeFalse("heartbeat must never fire when license is revoked on initial token fetch");
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
        TaskCompletionSource doneTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        FakeLicenseTokenClient licenseClient = new(onRequest: () =>
        {
            lock (callLock)
            {
                // Stamped INSIDE the lock. Read outside it, two heartbeat ticks
                // racing here could take their timestamps in one order and append
                // them in the other, and the list would not be monotonic — CI
                // measured a gap of -712ms and failed on a backoff that was
                // working perfectly.
                callTimes.Add(DateTime.UtcNow);
                if (callTimes.Count >= maxCalls)
                    doneTcs.TrySetResult();
            }
            return new(null, LicenseFailureKind.Unauthenticated, "401");
        });

        ClusterTokenHolder holder = new();
        // Ensure the token is always "near expiry" so every heartbeat tick
        // attempts a refresh.
        holder.Update(new("seed", DateTime.UtcNow.AddSeconds(30), []));

        RecordingHandler http = new();
        WorkerSelfRegistrationService sut = MakeService(
            http,
            opts =>
            {
                opts.DistributedEncodingSigningKey = "key";
                opts.CoordinatorUrl = "http://coordinator.test";
                opts.WorkerSelfBaseUrl = "http://worker.test";
                opts.WorkerId = "tw";
                // Very short heartbeat so the test doesn't take forever.
                opts.WorkerHeartbeatInterval = TimeSpan.FromMilliseconds(10);
            },
            holder: holder,
            licenseClient: licenseClient
        );

        using CancellationTokenSource cts = new();
        await sut.StartAsync(cts.Token);
        await doneTcs.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await cts.CancelAsync();
        await sut.StopAsync(CancellationToken.None);

        // The gaps between successive calls should be non-decreasing
        // (backoff grows).  We compare gap[1] > gap[0] as a minimal proof.
        callTimes.Count.Should().BeGreaterThanOrEqualTo(3);
        TimeSpan firstGap = callTimes[1] - callTimes[0];
        TimeSpan secondGap = callTimes[2] - callTimes[1];

        // The second gap should be at least as large as the first, because
        // the backoff delay doubles from 1s → 2s.
        secondGap
            .Should()
            .BeGreaterThanOrEqualTo(
                firstGap,
                "backoff must not decrease between consecutive 401 failures"
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
        configure(opts);

        ServiceCollection services = new();
        services.AddSingleton(opts);
        services.AddSingleton<IHardwareCapabilities>(new HardwareCapabilities([], 4));
        services
            .AddHttpClient()
            .ConfigureHttpClientDefaults(b =>
                b.ConfigurePrimaryHttpMessageHandler(() => httpHandler)
            );

        ServiceProvider provider = services.BuildServiceProvider();

        return new(
            provider.GetRequiredService<IHardwareCapabilities>(),
            opts,
            provider.GetRequiredService<IHttpClientFactory>(),
            NullLogger<WorkerSelfRegistrationService>.Instance,
            holder,
            licenseClient
        );
    }

    // ── Fakes ─────────────────────────────────────────────────────────────────

    private sealed class FakeLicenseTokenClient(Func<ClusterTokenResult> onRequest)
        : ILicenseTokenClient
    {
        public Task<ClusterTokenResult> RequestAsync(CancellationToken ct) =>
            Task.FromResult(onRequest());

        public Task<IntrospectResult> IntrospectAsync(string token, CancellationToken ct) =>
            Task.FromResult(new IntrospectResult(Active: true, Scopes: [], Message: null));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage>? respond = null)
        {
            _respond = respond ?? (_ => new(HttpStatusCode.OK));
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_respond(request));
        }
    }
}
