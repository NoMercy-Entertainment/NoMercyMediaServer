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

using NoMercy.NmSystem.Security;
using Microsoft.AspNetCore.DataProtection;
using NoMercy.NmSystem.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NoMercy.Api.Middleware;
using NoMercy.Database;
using NoMercy.Setup.Auth;
using NoMercy.Setup.Server;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Tests.Setup.Infrastructure;

namespace NoMercy.Tests.Setup;

public class SetupModeMiddlewareTests
{
    private static SetupEndpoints CreateSetupEndpoints(SetupState state)
    {
        ServiceCollection services = new();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        ServiceProvider provider = services.BuildServiceProvider();
        TokenStore.Initialize(serviceProvider: provider);

        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(connectionString: "Data Source=:memory:");
        AppDbContext dbContext = new(options: optionsBuilder.Options);
        dbContext.Database.OpenConnection();
        dbContext.Database.EnsureCreated();

        AuthManager authManager = new(appContext: dbContext, driver: new LocalStorageDriver(), authTokenStore: new AuthTokenStore());
        return new(state: state, authManager: authManager, serverRegistrationService: new FakeServerRegistrationService());
    }

    private static SetupModeMiddleware CreateMiddleware(
        SetupState state,
        RequestDelegate? next = null
    )
    {
        next ??= _ => Task.CompletedTask;
        SetupEndpoints setupEndpoints = CreateSetupEndpoints(state: state);
        return new(next: next, setupState: state, setupEndpoints: setupEndpoints);
    }

    private static DefaultHttpContext CreateContext(string path)
    {
        DefaultHttpContext context = new()
        {
            Request = { Path = path },
            Response = { Body = new MemoryStream() },
        };
        return context;
    }

    private static async Task<string> ReadResponseBody(HttpContext context)
    {
        context.Response.Body.Seek(offset: 0, origin: SeekOrigin.Begin);
        using StreamReader reader = new(stream: context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    // --- Setup required: non-setup routes return 503 ---

    [Fact]
    public async Task NonSetupRoute_WhenSetupRequired_Returns503()
    {
        SetupState state = new();
        SetupModeMiddleware middleware = CreateMiddleware(state: state);
        DefaultHttpContext context = CreateContext(path: "/api/v1/libraries");

        await middleware.InvokeAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status503ServiceUnavailable, actual: context.Response.StatusCode);
    }

    [Fact]
    public async Task NonSetupRoute_WhenSetupRequired_ReturnsJsonBody()
    {
        SetupState state = new();
        SetupModeMiddleware middleware = CreateMiddleware(state: state);
        DefaultHttpContext context = CreateContext(path: "/api/v1/libraries");

        await middleware.InvokeAsync(context: context);

        string body = await ReadResponseBody(context: context);
        dynamic? data = JsonConvert.DeserializeObject<dynamic>(value: body);

        Assert.NotNull(data);
        Assert.Equal(expected: "setup_required", actual: (string)data!.status);
        Assert.Equal(expected: "Server is in setup mode", actual: (string)data.message);
        Assert.Equal(expected: "/setup", actual: (string)data.setup_url);
    }

    [Fact]
    public async Task NonSetupRoute_WhenSetupRequired_SetsJsonContentType()
    {
        SetupState state = new();
        SetupModeMiddleware middleware = CreateMiddleware(state: state);
        DefaultHttpContext context = CreateContext(path: "/api/v1/movies");

        await middleware.InvokeAsync(context: context);

        Assert.Equal(expected: "application/json", actual: context.Response.ContentType);
    }

    [Fact]
    public async Task RootRoute_WhenSetupRequired_Returns503()
    {
        SetupState state = new();
        SetupModeMiddleware middleware = CreateMiddleware(state: state);
        DefaultHttpContext context = CreateContext(path: "/");

        await middleware.InvokeAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status503ServiceUnavailable, actual: context.Response.StatusCode);
    }

    [Fact]
    public async Task RandomPath_WhenSetupRequired_Returns503()
    {
        SetupState state = new();
        SetupModeMiddleware middleware = CreateMiddleware(state: state);
        DefaultHttpContext context = CreateContext(path: "/some/random/path");

        await middleware.InvokeAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status503ServiceUnavailable, actual: context.Response.StatusCode);
    }

    [Fact]
    public async Task NonSetupRoute_WhenSetupRequired_DoesNotCallNext()
    {
        SetupState state = new();
        bool nextCalled = false;
        SetupModeMiddleware middleware = CreateMiddleware(
            state: state,
            next: _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            }
        );
        DefaultHttpContext context = CreateContext(path: "/api/v1/libraries");

        await middleware.InvokeAsync(context: context);

        Assert.False(condition: nextCalled);
    }

    // --- Setup required: setup routes pass through ---

    [Fact]
    public async Task SetupRoute_WhenSetupRequired_IsHandledDirectly()
    {
        SetupState state = new();
        bool nextCalled = false;
        SetupModeMiddleware middleware = CreateMiddleware(
            state: state,
            next: _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            }
        );
        DefaultHttpContext context = CreateContext(path: "/setup");

        await middleware.InvokeAsync(context: context);

        Assert.False(condition: nextCalled);
        Assert.NotEqual(expected: StatusCodes.Status503ServiceUnavailable, actual: context.Response.StatusCode);
    }

    [Fact]
    public async Task SetupStatusRoute_WhenSetupRequired_IsHandledDirectly()
    {
        SetupState state = new();
        bool nextCalled = false;
        SetupModeMiddleware middleware = CreateMiddleware(
            state: state,
            next: _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            }
        );
        DefaultHttpContext context = CreateContext(path: "/setup/status");

        await middleware.InvokeAsync(context: context);

        Assert.False(condition: nextCalled);
        Assert.NotEqual(expected: StatusCodes.Status503ServiceUnavailable, actual: context.Response.StatusCode);
    }

    [Fact]
    public async Task SetupConfigRoute_WhenSetupRequired_IsHandledDirectly()
    {
        SetupState state = new();
        bool nextCalled = false;
        SetupModeMiddleware middleware = CreateMiddleware(
            state: state,
            next: _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            }
        );
        DefaultHttpContext context = CreateContext(path: "/setup/config");

        await middleware.InvokeAsync(context: context);

        Assert.False(condition: nextCalled);
        Assert.NotEqual(expected: StatusCodes.Status503ServiceUnavailable, actual: context.Response.StatusCode);
    }

    [Fact]
    public async Task SsoCallbackRoute_WhenSetupRequired_IsHandledDirectly()
    {
        SetupState state = new();
        bool nextCalled = false;
        SetupModeMiddleware middleware = CreateMiddleware(
            state: state,
            next: _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            }
        );
        DefaultHttpContext context = CreateContext(path: "/sso-callback");

        await middleware.InvokeAsync(context: context);

        Assert.False(condition: nextCalled);
        Assert.NotEqual(expected: StatusCodes.Status503ServiceUnavailable, actual: context.Response.StatusCode);
    }

    [Fact]
    public async Task HealthRoute_WhenSetupRequired_CallsNext()
    {
        SetupState state = new();
        bool nextCalled = false;
        SetupModeMiddleware middleware = CreateMiddleware(
            state: state,
            next: _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            }
        );
        DefaultHttpContext context = CreateContext(path: "/health");

        await middleware.InvokeAsync(context: context);

        Assert.True(condition: nextCalled);
    }

    // --- Setup complete: all routes pass through ---

    [Fact]
    public async Task AnyRoute_WhenSetupComplete_CallsNext()
    {
        SetupState state = new();
        state.DetermineInitialPhase(hasValidToken: true, isRegistered: true);

        bool nextCalled = false;
        SetupModeMiddleware middleware = CreateMiddleware(
            state: state,
            next: _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            }
        );
        DefaultHttpContext context = CreateContext(path: "/api/v1/libraries");

        await middleware.InvokeAsync(context: context);

        Assert.True(condition: nextCalled);
    }

    [Fact]
    public async Task AnyRoute_WhenSetupComplete_DoesNotReturn503()
    {
        SetupState state = new();
        state.DetermineInitialPhase(hasValidToken: true, isRegistered: true);

        SetupModeMiddleware middleware = CreateMiddleware(state: state);
        DefaultHttpContext context = CreateContext(path: "/api/v1/libraries");

        await middleware.InvokeAsync(context: context);

        Assert.NotEqual(expected: StatusCodes.Status503ServiceUnavailable, actual: context.Response.StatusCode);
    }

    // --- Trailing slash handling ---

    [Fact]
    public async Task SetupRouteWithTrailingSlash_WhenSetupRequired_IsHandledDirectly()
    {
        SetupState state = new();
        bool nextCalled = false;
        SetupModeMiddleware middleware = CreateMiddleware(
            state: state,
            next: _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            }
        );
        DefaultHttpContext context = CreateContext(path: "/setup/");

        await middleware.InvokeAsync(context: context);

        Assert.False(condition: nextCalled);
        Assert.NotEqual(expected: StatusCodes.Status503ServiceUnavailable, actual: context.Response.StatusCode);
    }

    // --- Case insensitivity ---

    [Fact]
    public async Task SetupRouteUpperCase_WhenSetupRequired_IsHandledDirectly()
    {
        SetupState state = new();
        bool nextCalled = false;
        SetupModeMiddleware middleware = CreateMiddleware(
            state: state,
            next: _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            }
        );
        DefaultHttpContext context = CreateContext(path: "/Setup");

        await middleware.InvokeAsync(context: context);

        Assert.False(condition: nextCalled);
        Assert.NotEqual(expected: StatusCodes.Status503ServiceUnavailable, actual: context.Response.StatusCode);
    }

    // --- SignalR hubs blocked during setup ---

    [Fact]
    public async Task SignalRHub_WhenSetupRequired_Returns503()
    {
        SetupState state = new();
        SetupModeMiddleware middleware = CreateMiddleware(state: state);
        DefaultHttpContext context = CreateContext(path: "/videoHub");

        await middleware.InvokeAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status503ServiceUnavailable, actual: context.Response.StatusCode);
    }

    [Fact]
    public async Task SwaggerRoute_WhenSetupRequired_Returns503()
    {
        SetupState state = new();
        SetupModeMiddleware middleware = CreateMiddleware(state: state);
        DefaultHttpContext context = CreateContext(path: "/swagger");

        await middleware.InvokeAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status503ServiceUnavailable, actual: context.Response.StatusCode);
    }
}
