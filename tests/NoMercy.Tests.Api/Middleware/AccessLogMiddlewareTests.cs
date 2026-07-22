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
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Api.Middleware;
using NoMercy.Authorization;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Middleware;

/// <summary>
/// Coverage for AccessLogMiddleware's gating: ignored route prefixes/exact
/// routes/folder paths skip both auth AND logging, the guest/malformed-guid
/// 401 problem responses (with the single "/status" ignoreIfGuest exception),
/// and the cache-miss-triggers-RefreshUsersAsync retry path. Uses
/// NoMercyApiFactory for its seeded UserCache/MediaContext DI wiring.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class AccessLogMiddlewareTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public AccessLogMiddlewareTests(NoMercyApiFactory factory)
    {
        _factory = factory;
        _factory.CreateClient();
    }

    private static AccessLogMiddleware CreateMiddleware(out StrongBox<bool> nextCalled)
    {
        StrongBox<bool> called = new(value: false);
        AccessLogMiddleware middleware = new(
            next: _ =>
            {
                called.Value = true;
                return Task.CompletedTask;
            },
            logger: NullLogger<AccessLogMiddleware>.Instance
        );
        nextCalled = called;
        return middleware;
    }

    private DefaultHttpContext MakeContext(
        string path,
        ClaimsPrincipal? user = null,
        bool withRequestServices = false
    )
    {
        DefaultHttpContext context = new() { RequestServices = null! };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        if (user is not null)
            context.User = user;
        if (withRequestServices)
            context.RequestServices = _factory.Services.CreateScope().ServiceProvider;
        return context;
    }

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(offset: 0, origin: SeekOrigin.Begin);
        using StreamReader reader = new(stream: context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    // =========================================================================
    // Ignored routes: never touch auth, always call next()
    // =========================================================================

    [Theory]
    [InlineData(data: "/images/poster.jpg")]
    [InlineData(data: "/swagger/index.html")]
    [InlineData(data: "/videoHub/negotiate")]
    [InlineData(data: "/dashboardHub")]
    public async Task IgnoredStartsWithRoute_CallsNext_NoAuthRequired(string path)
    {
        AccessLogMiddleware middleware = CreateMiddleware(nextCalled: out StrongBox<bool> nextCalled);
        DefaultHttpContext context = MakeContext(path: path);

        await middleware.InvokeAsync(context: context);

        nextCalled.Value.Should().BeTrue();
    }

    [Theory]
    [InlineData(data: "/")]
    [InlineData(data: "/api/v1/dashboard/logs")]
    [InlineData(data: "/api/v1/setup/server-info")]
    public async Task IgnoredExactRoute_CallsNext_NoAuthRequired(string path)
    {
        AccessLogMiddleware middleware = CreateMiddleware(nextCalled: out StrongBox<bool> nextCalled);
        DefaultHttpContext context = MakeContext(path: path);

        await middleware.InvokeAsync(context: context);

        nextCalled.Value.Should().BeTrue();
    }

    [Fact]
    public async Task FolderScopedPath_CallsNext_NoAuthRequired()
    {
        AccessLogMiddleware middleware = CreateMiddleware(nextCalled: out StrongBox<bool> nextCalled);
        DefaultHttpContext context = MakeContext(path: $"/{NoMercyApiFactory.MovieFolderId}/movie.m3u8");

        await middleware.InvokeAsync(context: context);

        nextCalled.Value.Should().BeTrue();
    }

    // =========================================================================
    // No / malformed GUID claim
    // =========================================================================

    [Fact]
    public async Task NoClaim_NonGuestRoute_Returns401NoToken()
    {
        AccessLogMiddleware middleware = CreateMiddleware(nextCalled: out StrongBox<bool> nextCalled);
        DefaultHttpContext context = MakeContext(
            path: "/api/v1/media/movies",
            user: new ClaimsPrincipal(identity: new ClaimsIdentity())
        );

        await middleware.InvokeAsync(context: context);

        nextCalled.Value.Should().BeFalse();
        context.Response.StatusCode.Should().Be(expected: (int)HttpStatusCode.Unauthorized);
        string body = await ReadBodyAsync(context: context);
        using JsonDocument json = JsonDocument.Parse(json: body);
        json.RootElement.GetProperty(propertyName: "authError").GetString().Should().Be(expected: "NO_TOKEN");
    }

    [Fact]
    public async Task NoClaim_StatusRoute_IsIgnoreIfGuest_CallsNext()
    {
        AccessLogMiddleware middleware = CreateMiddleware(nextCalled: out StrongBox<bool> nextCalled);
        DefaultHttpContext context = MakeContext(
            path: "/status",
            user: new ClaimsPrincipal(identity: new ClaimsIdentity())
        );

        await middleware.InvokeAsync(context: context);

        nextCalled.Value.Should().BeTrue();
    }

    [Fact]
    public async Task MalformedGuidClaim_NonGuestRoute_Returns401InvalidToken()
    {
        AccessLogMiddleware middleware = CreateMiddleware(nextCalled: out StrongBox<bool> nextCalled);
        ClaimsPrincipal principal = new(
            identity: new ClaimsIdentity(claims: [new(type: ClaimTypes.NameIdentifier, value: "not-a-guid")])
        );
        DefaultHttpContext context = MakeContext(path: "/api/v1/media/movies", user: principal);

        await middleware.InvokeAsync(context: context);

        nextCalled.Value.Should().BeFalse();
        context.Response.StatusCode.Should().Be(expected: (int)HttpStatusCode.Unauthorized);
        string body = await ReadBodyAsync(context: context);
        using JsonDocument json = JsonDocument.Parse(json: body);
        json.RootElement.GetProperty(propertyName: "authError").GetString().Should().Be(expected: "INVALID_TOKEN");
    }

    [Fact]
    public async Task MalformedGuidClaim_StatusRoute_IsIgnoreIfGuest_CallsNext()
    {
        AccessLogMiddleware middleware = CreateMiddleware(nextCalled: out StrongBox<bool> nextCalled);
        ClaimsPrincipal principal = new(
            identity: new ClaimsIdentity(claims: [new(type: ClaimTypes.NameIdentifier, value: "not-a-guid")])
        );
        DefaultHttpContext context = MakeContext(path: "/status", user: principal);

        await middleware.InvokeAsync(context: context);

        nextCalled.Value.Should().BeTrue();
    }

    [Fact]
    public async Task EmptyGuidClaim_NonGuestRoute_Returns401InvalidToken()
    {
        AccessLogMiddleware middleware = CreateMiddleware(nextCalled: out StrongBox<bool> nextCalled);
        ClaimsPrincipal principal = new(
            identity: new ClaimsIdentity(claims: [new(type: ClaimTypes.NameIdentifier, value: Guid.Empty.ToString())])
        );
        DefaultHttpContext context = MakeContext(path: "/api/v1/media/movies", user: principal);

        await middleware.InvokeAsync(context: context);

        nextCalled.Value.Should().BeFalse();
        context.Response.StatusCode.Should().Be(expected: (int)HttpStatusCode.Unauthorized);
    }

    // =========================================================================
    // Valid GUID: known user vs unknown user (with the cache-miss refresh retry)
    // =========================================================================

    [Fact]
    public async Task KnownSeededUser_CallsNext()
    {
        AccessLogMiddleware middleware = CreateMiddleware(nextCalled: out StrongBox<bool> nextCalled);
        ClaimsPrincipal principal = new(
            identity: new ClaimsIdentity(claims:
            [
                new(type: ClaimTypes.NameIdentifier, value: TestAuthHandler.DefaultUserId.ToString()),
            ])
        );
        DefaultHttpContext context = MakeContext(
            path: "/api/v1/media/movies",
            user: principal,
            withRequestServices: true
        );

        await middleware.InvokeAsync(context: context);

        nextCalled.Value.Should().BeTrue();
    }

    [Fact]
    public async Task UserNotInCache_ButExistsInDatabase_RefreshRetrySucceeds_CallsNext()
    {
        // Regression coverage for the cache-miss retry: insert a user directly
        // into the database WITHOUT adding it to UserCache first — simulating
        // the real race the comment on line ~174 describes (cache not yet
        // populated at startup). The middleware's RefreshUsersAsync fallback
        // must find it and let the request through.
        IDbContextFactory<MediaContext> contextFactory = _factory.Services.GetRequiredService<
            IDbContextFactory<MediaContext>
        >();
        await using MediaContext ctx = await contextFactory.CreateDbContextAsync();
        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@nomercy.tv",
            Name = "Cache Miss Test User",
            Owner = false,
            Allowed = true,
            Manage = false,
        };
        ctx.Users.Add(entity: user);
        await ctx.SaveChangesAsync();

        try
        {
            AccessLogMiddleware middleware = CreateMiddleware(nextCalled: out StrongBox<bool> nextCalled);
            ClaimsPrincipal principal = new(
                identity: new ClaimsIdentity(claims: [new(type: ClaimTypes.NameIdentifier, value: user.Id.ToString())])
            );
            DefaultHttpContext context = MakeContext(
                path: "/api/v1/media/movies",
                user: principal,
                withRequestServices: true
            );

            await middleware.InvokeAsync(context: context);

            nextCalled.Value.Should().BeTrue();
        }
        finally
        {
            // Leave the DB row (harmless, isolated by fresh Guid) but keep the
            // process-wide UserCache from accumulating test users indefinitely.
            User? cached = UserCache.Current.GetUser(userId: user.Id);
            if (cached is not null)
                UserCache.Current.RemoveUser(user: cached);
        }
    }

    [Fact]
    public async Task UnknownUser_NotInCacheOrDatabase_Returns401UserNotFound()
    {
        AccessLogMiddleware middleware = CreateMiddleware(nextCalled: out StrongBox<bool> nextCalled);
        ClaimsPrincipal principal = new(
            identity: new ClaimsIdentity(claims: [new(type: ClaimTypes.NameIdentifier, value: Guid.NewGuid().ToString())])
        );
        DefaultHttpContext context = MakeContext(
            path: "/api/v1/media/movies",
            user: principal,
            withRequestServices: true
        );

        await middleware.InvokeAsync(context: context);

        nextCalled.Value.Should().BeFalse();
        context.Response.StatusCode.Should().Be(expected: (int)HttpStatusCode.Unauthorized);
        string body = await ReadBodyAsync(context: context);
        using JsonDocument json = JsonDocument.Parse(json: body);
        json.RootElement.GetProperty(propertyName: "authError").GetString().Should().Be(expected: "USER_NOT_FOUND");
    }
}
