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

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Networking.Certificate;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Status;
using NoMercy.Setup.Auth;
using NoMercy.Setup.Dto;
using NoMercy.Tests.Setup.Infrastructure;

namespace NoMercy.Tests.Setup.Auth;

/// <summary>
/// Requirement: registering a self-hosted server with nomercy-tv must retry
/// transient failures, stop immediately on a 401 (re-auth is required, not more
/// retries), share a single in-flight attempt across concurrent callers, and enter
/// a cooldown after a failure so a boot loop cannot hammer the registration API.
/// </summary>
/// <remarks>
/// <see cref="ServerRegistrationService"/> builds its own <c>GenericHttpClient</c>
/// pointed at <c>ExternalServicesConfig.Current.ApiServerBaseUrl</c> rather than
/// accepting an injectable client — a real loopback <see cref="LoopbackHttpServer"/>
/// exercises the actual HTTP contract without a live network dependency.
/// Tests use <c>maxRetries: 1</c> wherever the outcome isn't the retry behavior itself,
/// since the real backoff schedule (2s/5s/15s/30s/60s) uses genuine <see cref="Task.Delay"/>
/// with no injectable clock.
/// </remarks>
[Trait(name: "Category", value: "Unit")]
public sealed class ServerRegistrationServiceHttpTests : IDisposable
{
    private readonly List<SqliteConnection> _connections = [];

    public void Dispose()
    {
        foreach (SqliteConnection connection in _connections)
            connection.Dispose();
    }

    private Microsoft.EntityFrameworkCore.IDbContextFactory<AppDbContext> CreateFactory()
    {
        SqliteConnection connection = new(connectionString: "DataSource=:memory:;Foreign Keys=False");
        connection.Open();
        _connections.Add(item: connection);

        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection: connection)
            .Options;

        using (AppDbContext init = new(options: options))
            init.Database.EnsureCreated();

        Mock<Microsoft.EntityFrameworkCore.IDbContextFactory<AppDbContext>> mock = new();
        mock.Setup(expression: x => x.CreateDbContext()).Returns(valueFunction: () => new(options: options));
        mock.Setup(expression: x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(valueFunction: () => new(options: options));
        return mock.Object;
    }

    private static ServerRegistrationService Build(
        Microsoft.EntityFrameworkCore.IDbContextFactory<AppDbContext> factory,
        IUserProvisioningService? provisioning = null,
        ICertificateService? certificate = null,
        IConnectivityStatus? connectivity = null,
        INetworkDiscovery? networkDiscovery = null
    )
    {
        Mock<ICertificateService> certMock = new();
        certMock
            .Setup(expression: c => c.RenewSslCertificate(It.IsAny<string?>(), It.IsAny<int>()))
            .Returns(value: Task.CompletedTask);

        return new(
            authTokenStore: new AuthTokenStore(),
            appDbContextFactory: factory,
            userProvisioningService: provisioning ?? Mock.Of<IUserProvisioningService>(),
            connectivityStatus: connectivity ?? new ConnectivityStatus(),
            certificateService: certificate ?? certMock.Object,
            networkDiscovery: networkDiscovery
        );
    }

    private static string ServerRegisterResponseJson(string status = "ok") =>
        JsonConvert.SerializeObject(
            value: new ServerRegisterResponse
            {
                Data = new()
                {
                    Status = status,
                    User = new()
                    {
                        Id = Guid.NewGuid(),
                        Name = "Owner",
                        Email = "owner@nomercy.tv",
                    },
                },
            }
        );

    // ── AssignServer / AssignServerWithRetry ────────────────────────────────

    [Fact]
    public async Task AssignServerWithRetry_Success_ProvisionsOwnerOnce()
    {
        Mock<IUserProvisioningService> provisioning = new();
        provisioning.Setup(expression: p => p.ProvisionOwner(It.IsAny<User>())).Returns(value: Task.CompletedTask);

        using LoopbackHttpServer server = new();
        server.Handler = req =>
            req.Path.EndsWith(value: "/assign") ? new(StatusCode: 200, Body: ServerRegisterResponseJson()) : new(StatusCode: 404, Body: "");
        using ExternalServicesConfigScope scope = new(apiServerBaseUrl: server.BaseUrl);

        ServerRegistrationService service = Build(factory: CreateFactory(), provisioning: provisioning.Object);

        // Init() runs RegisterServer -> AssignServerWithRetry -> RenewSslCertificate.
        // The register call also hits /register on the same server; make it succeed too.
        server.Handler = req => new(StatusCode: 200, Body: ServerRegisterResponseJson());

        await service.Init(maxRetries: 1);

        provisioning.Verify(expression: p => p.ProvisionOwner(It.IsAny<User>()), times: Times.Once);
    }

    [Fact]
    public async Task AssignServer_ErrorStatus_ThrowsFailedToAssign()
    {
        using LoopbackHttpServer server = new();
        server.Handler = req =>
            req.Path.EndsWith(value: "/register")
                ? new(StatusCode: 200, Body: ServerRegisterResponseJson())
                : new(StatusCode: 200, Body: ServerRegisterResponseJson(status: "error"));
        using ExternalServicesConfigScope scope = new(apiServerBaseUrl: server.BaseUrl);

        ServerRegistrationService service = Build(factory: CreateFactory());

        // AssignServer's `throw new("Failed to assign Server")` is a plain Exception
        // (no explicit type before the target-typed `new`) — not InvalidOperationException.
        Exception ex = await Assert.ThrowsAsync<Exception>(testCode: () => service.Init(maxRetries: 1));
        Assert.Equal(expected: "Failed to assign Server", actual: ex.Message);
    }

    [Fact]
    public async Task AssignServerWithRetry_401Unauthorized_StopsWithoutExhaustingRetries()
    {
        int assignAttempts = 0;
        using LoopbackHttpServer server = new();
        server.Handler = req =>
        {
            if (req.Path.EndsWith(value: "/register"))
                return new(StatusCode: 200, Body: ServerRegisterResponseJson());
            Interlocked.Increment(location: ref assignAttempts);
            return new(StatusCode: 401, Body: "unauthorized");
        };
        using ExternalServicesConfigScope scope = new(apiServerBaseUrl: server.BaseUrl);

        ServerRegistrationService service = Build(factory: CreateFactory());

        // maxRetries: 3 would otherwise sleep through the real 2s/5s backoff between
        // attempts; the 401 short-circuit means only ONE assign attempt should ever
        // fire regardless of the retry budget, so this proves the break — not the loop.
        await service.Init(maxRetries: 3);

        Assert.Equal(expected: 1, actual: assignAttempts);
    }

    // ── RegisterServer 401 short-circuit ─────────────────────────────────────

    [Fact]
    public async Task RegisterServer_401Unauthorized_StopsWithoutExhaustingRetries()
    {
        int registerAttempts = 0;
        using LoopbackHttpServer server = new();
        server.Handler = req =>
        {
            if (req.Path.EndsWith(value: "/register"))
                Interlocked.Increment(location: ref registerAttempts);
            return new(StatusCode: 401, Body: "unauthorized");
        };
        using ExternalServicesConfigScope scope = new(apiServerBaseUrl: server.BaseUrl);

        ServerRegistrationService service = Build(factory: CreateFactory());

        // RegisterServer's 401 handling is a silent `break` (log + return), not a
        // throw — the SAME 401 also short-circuits AssignServerWithRetry immediately
        // after (both hit the same server here), so the whole Init() call completes
        // without throwing at all. Needs maxRetries >= 2: with maxRetries=1 the
        // `when (attempt < maxRetries)` catch filter never matches and the exception
        // propagates uncaught instead (covered by RegisterServer_SingleAttemptFailure_PropagatesException).
        await service.Init(maxRetries: 3);

        Assert.Equal(expected: 1, actual: registerAttempts);
    }

    [Fact]
    public async Task RegisterServer_SingleAttemptFailure_PropagatesException()
    {
        using LoopbackHttpServer server = new();
        server.Handler = _ => new(StatusCode: 400, Body: "bad request");
        using ExternalServicesConfigScope scope = new(apiServerBaseUrl: server.BaseUrl);

        ServerRegistrationService service = Build(factory: CreateFactory());

        // maxRetries: 1 means the `when (attempt < maxRetries)` catch filter never
        // matches on the only attempt, so the underlying HTTP exception propagates
        // out of RegisterServer -> RunRegistrationAsync -> Init unchanged.
        await Assert.ThrowsAsync<HttpRequestException>(testCode: () => service.Init(maxRetries: 1));
    }

    // ── Retry-then-succeed (accepts one real ~2s backoff wait) ───────────────

    [Fact]
    public async Task RegisterServer_TransientFailureThenSuccess_Retries()
    {
        int attempt = 0;
        using LoopbackHttpServer server = new();
        server.Handler = req =>
        {
            if (!req.Path.EndsWith(value: "/register"))
                return new(StatusCode: 200, Body: ServerRegisterResponseJson());

            int thisAttempt = Interlocked.Increment(location: ref attempt);
            // 400 (not 500/429/408): GenericHttpClient's own Polly policy retries
            // transient 5xx/408/429 internally with its own 2s/4s/8s backoff, which
            // would stack on top of ServerRegistrationService's outer retry and make
            // this test needlessly slow. A 400 fails the inner client immediately,
            // isolating the OUTER retry-then-succeed behavior this test targets.
            return thisAttempt == 1 ? new(StatusCode: 400, Body: "bad request") : new(StatusCode: 200, Body: "{}");
        };
        using ExternalServicesConfigScope scope = new(apiServerBaseUrl: server.BaseUrl);

        ServerRegistrationService service = Build(factory: CreateFactory());

        await service.Init(maxRetries: 2);

        Assert.Equal(expected: 2, actual: attempt);
    }

    // ── Cooldown after failure ──────────────────────────────────────────────

    [Fact]
    public async Task Init_AfterRecentFailure_ThrowsCooldownWithoutCallingNetwork()
    {
        using LoopbackHttpServer server = new();
        server.Handler = _ => new(StatusCode: 400, Body: "bad request");
        using ExternalServicesConfigScope scope = new(apiServerBaseUrl: server.BaseUrl);

        ServerRegistrationService service = Build(factory: CreateFactory());

        // First call fails (single attempt, no retry) and starts the cooldown. The
        // underlying HTTP failure propagates unchanged (see the SingleAttemptFailure
        // test above) — what this test locks is the SECOND call's behavior.
        await Assert.ThrowsAsync<HttpRequestException>(testCode: () => service.Init(maxRetries: 1));

        int requestsAfterFirstFailure = server.RequestCount;

        // Immediately calling again must hit the cooldown guard, not the network.
        await Assert.ThrowsAsync<InvalidOperationException>(testCode: () => service.Init(maxRetries: 1));

        Assert.Equal(expected: requestsAfterFirstFailure, actual: server.RequestCount);
    }

    // ── Concurrent callers share the in-flight attempt ──────────────────────

    [Fact]
    public async Task Init_ConcurrentCallers_ShareSingleInFlightAttempt()
    {
        using LoopbackHttpServer server = new();
        server.Handler = req =>
        {
            // Slow enough that both callers are guaranteed to observe the same
            // in-flight Task rather than racing to completion before either checks.
            Thread.Sleep(millisecondsTimeout: 100);
            return new LoopbackResponse(StatusCode: 200, Body: ServerRegisterResponseJson());
        };
        using ExternalServicesConfigScope scope = new(apiServerBaseUrl: server.BaseUrl);

        ServerRegistrationService service = Build(factory: CreateFactory());

        Task first = service.Init(maxRetries: 1);
        Task second = service.Init(maxRetries: 1);

        await Task.WhenAll(tasks: [first, second]);

        Assert.Same(expected: first, actual: second);
    }

    // ── GetTunnelAvailability ────────────────────────────────────────────────

    [Fact]
    public async Task GetTunnelAvailability_Allowed_SetsCloudflareTunnelToken()
    {
        using LoopbackHttpServer server = new();
        server.Handler = _ =>
            new(
                StatusCode: 200,
                Body: JsonConvert.SerializeObject(
                    value: new ServerTunnelAvailabilityResponse
                    {
                        Allowed = true,
                        Token = "tunnel-token-1",
                    }
                )
            );
        using ExternalServicesConfigScope scope = new(apiServerBaseUrl: server.BaseUrl);

        ConnectivityStatus status = new();
        ServerRegistrationService service = Build(factory: CreateFactory(), connectivity: status);

        await service.GetTunnelAvailability();

        Assert.Equal(expected: "tunnel-token-1", actual: status.CloudflareTunnelToken);
    }

    [Fact]
    public async Task GetTunnelAvailability_NotAllowed_DoesNotSetToken()
    {
        using LoopbackHttpServer server = new();
        server.Handler = _ =>
            new(
                StatusCode: 200,
                Body: JsonConvert.SerializeObject(
                    value: new ServerTunnelAvailabilityResponse { Allowed = false, Token = null }
                )
            );
        using ExternalServicesConfigScope scope = new(apiServerBaseUrl: server.BaseUrl);

        ConnectivityStatus status = new();
        ServerRegistrationService service = Build(factory: CreateFactory(), connectivity: status);

        await service.GetTunnelAvailability();

        Assert.Null(@object: status.CloudflareTunnelToken);
    }

    [Fact]
    public async Task GetTunnelAvailability_NetworkError_DoesNotThrow()
    {
        using ExternalServicesConfigScope scope = new(apiServerBaseUrl: "http://127.0.0.1:1/");

        ConnectivityStatus status = new();
        ServerRegistrationService service = Build(factory: CreateFactory(), connectivity: status);

        // GetTunnelAvailability wraps every failure — must never throw or crash the caller.
        await service.GetTunnelAvailability();

        Assert.Null(@object: status.CloudflareTunnelToken);
    }

    // ── GetDeviceName ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetServerInfo_ServerNameConfigured_UsesConfiguredName()
    {
        Microsoft.EntityFrameworkCore.IDbContextFactory<AppDbContext> factory = CreateFactory();
        await using (AppDbContext seed = await factory.CreateDbContextAsync())
        {
            seed.Configuration.Add(entity: new() { Key = "serverName", Value = "My Custom Server Name" });
            await seed.SaveChangesAsync();
        }

        using LoopbackHttpServer server = new();
        LoopbackRequest? captured = null;
        server.Handler = req =>
        {
            if (req.Path.EndsWith(value: "/register"))
                captured = req;
            return new(StatusCode: 200, Body: ServerRegisterResponseJson());
        };
        using ExternalServicesConfigScope scope = new(apiServerBaseUrl: server.BaseUrl);

        ServerRegistrationService service = Build(factory: factory);
        await service.Init(maxRetries: 1);

        Assert.NotNull(@object: captured);
        Assert.Contains(expectedSubstring: "My+Custom+Server+Name", actualString: captured!.Body);
    }
}
