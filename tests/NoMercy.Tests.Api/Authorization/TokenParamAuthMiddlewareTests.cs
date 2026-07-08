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

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Api.Middleware;
using NoMercy.Authorization;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Storage;
using NoMercy.Database.Models.Users;
using Xunit;

// ReSharper disable AccessToDisposedClosure

namespace NoMercy.Tests.Api.Authorization;

[Trait("Category", "Authorization")]
public sealed class TokenParamAuthMiddlewareTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MediaContext> _dbOptions;

    private static readonly Ulid KnownFolderId = Ulid.NewUlid();
    private static readonly Guid KnownUserId = Guid.NewGuid();

    public TokenParamAuthMiddlewareTests()
    {
        _connection = new(
            $"DataSource={Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );
        _connection.Open();

        _dbOptions = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new SqliteNormalizeSearchInterceptor())
            .Options;

        using MediaContext ctx = new(_dbOptions);
        ctx.Database.EnsureCreated();

        Driver driver = new()
        {
            Id = Driver.SystemLocalDriverId,
            Name = "Local",
            Type = "local",
            Config = "{}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Drivers.Add(driver);

        Folder folder = new()
        {
            Id = KnownFolderId,
            Path = "/media",
            DriverId = Driver.SystemLocalDriverId,
        };
        ctx.Folders.Add(folder);

        User user = new()
        {
            Id = KnownUserId,
            Name = "Known",
            Email = "k@nm.tv",
            Allowed = true,
        };
        ctx.Users.Add(user);
        ctx.SaveChanges();
    }

    public async Task InitializeAsync()
    {
        UserCache.Current.Reset();

        await using MediaContext ctx = new(_dbOptions);
        await UserCache.Current.InitializeAsync(ctx);
    }

    public Task DisposeAsync()
    {
        UserCache.Current.Reset();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    private static TokenParamAuthMiddleware BuildMiddleware(RequestDelegate next)
    {
        return new(next, NullLogger<TokenParamAuthMiddleware>.Instance);
    }

    private static HttpContext BuildContext(
        string path,
        string? authorizationHeader = null,
        ClaimsPrincipal? user = null
    )
    {
        DefaultHttpContext context = new();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        if (authorizationHeader is not null)
            context.Request.Headers.Authorization = authorizationHeader;

        if (user is not null)
            context.User = user;

        return context;
    }

    private static ClaimsPrincipal PrincipalWithSub(string sub)
    {
        List<Claim> claims = [new(ClaimTypes.NameIdentifier, sub)];
        return new(new ClaimsIdentity(claims, "TestScheme"));
    }

    [Fact]
    public async Task Allows_WhenUrlIsNotAFolderPath()
    {
        bool nextCalled = false;
        TokenParamAuthMiddleware middleware = BuildMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        HttpContext context = BuildContext("/api/v1/media/movies");

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Allows_WhenFolderPathAndBearerTokenPresent()
    {
        bool nextCalled = false;
        TokenParamAuthMiddleware middleware = BuildMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        HttpContext context = BuildContext(
            $"/{KnownFolderId}/some-file.mkv",
            authorizationHeader: "Bearer eyJhbGciOiJSUzI1NiJ9.test.sig"
        );

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Denies_WithUnauthorized_WhenFolderPathAndNoAuth()
    {
        bool nextCalled = false;
        TokenParamAuthMiddleware middleware = BuildMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        HttpContext context = BuildContext($"/{KnownFolderId}/some-file.mkv");

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Denies_WithForbidden_WhenFolderPathAndSubMalformed()
    {
        bool nextCalled = false;
        TokenParamAuthMiddleware middleware = BuildMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        HttpContext context = BuildContext(
            $"/{KnownFolderId}/some-file.mkv",
            user: PrincipalWithSub("not-a-guid")
        );

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Denies_WithForbidden_WhenFolderPathAndUserNotInCache()
    {
        bool nextCalled = false;
        TokenParamAuthMiddleware middleware = BuildMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        Guid unknownId = Guid.NewGuid();
        HttpContext context = BuildContext(
            $"/{KnownFolderId}/some-file.mkv",
            user: PrincipalWithSub(unknownId.ToString())
        );

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Allows_WhenFolderPathAndKnownUserInCacheWithNoBearer()
    {
        bool nextCalled = false;
        TokenParamAuthMiddleware middleware = BuildMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        HttpContext context = BuildContext(
            $"/{KnownFolderId}/some-file.mkv",
            user: PrincipalWithSub(KnownUserId.ToString())
        );

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }
}
