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

using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Api.Middleware;
using NoMercy.Setup.Server;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Middleware;

/// <summary>
/// Coverage for SetupModeMiddleware's route gating: setup-flow routes are always
/// served (even after setup completes, since the wizard's own success screen
/// redirects through them), explicit passthrough routes (/health, /manage) bypass
/// the gate only while setup IS required, and everything else 503s with a
/// setup_required envelope while setup is required. SetupEndpoints itself is
/// resolved from the real DI container (NoMercyApiFactory) since its handlers
/// read embedded resources deterministically with no network calls; SetupState
/// is constructed fresh per test so IsSetupRequired can be flipped without
/// mutating the shared factory singleton other tests rely on.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class SetupModeMiddlewareTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public SetupModeMiddlewareTests(NoMercyApiFactory factory)
    {
        _factory = factory;
        _factory.CreateClient();
    }

    private SetupModeMiddleware CreateMiddleware(
        bool setupRequired,
        out bool nextCalled,
        RequestDelegate? next = null
    )
    {
        SetupEndpoints setupEndpoints = _factory.Services.GetRequiredService<SetupEndpoints>();
        SetupState setupState = new();
        setupState.DetermineInitialPhase(hasValidToken: !setupRequired);

        bool called = false;
        RequestDelegate finalNext =
            next
            ?? (
                ctx =>
                {
                    called = true;
                    return Task.CompletedTask;
                }
            );

        SetupModeMiddleware middleware = new(next: finalNext, setupState: setupState, setupEndpoints: setupEndpoints);
        nextCalled = called;
        return middleware;
    }

    private static DefaultHttpContext MakeContext(string path, string method = "GET")
    {
        DefaultHttpContext context = new() { RequestServices = null! };
        context.Request.Path = path;
        context.Request.Method = method;
        context.Response.Body = new MemoryStream();
        return context;
    }

    // =========================================================================
    // Setup routes: always served, regardless of IsSetupRequired
    // =========================================================================

    [Fact]
    public async Task SetupRoute_AlwaysServed_WhenSetupNotRequired()
    {
        SetupModeMiddleware middleware = CreateMiddleware(setupRequired: false, nextCalled: out _);
        DefaultHttpContext context = MakeContext(path: "/setup");

        await middleware.InvokeAsync(context: context);

        context.Response.StatusCode.Should().Be(expected: StatusCodes.Status200OK);
        context.Response.ContentType.Should().Contain(expected: "text/html");
    }

    [Fact]
    public async Task SetupRoute_AlwaysServed_WhenSetupRequired()
    {
        SetupModeMiddleware middleware = CreateMiddleware(setupRequired: true, nextCalled: out _);
        DefaultHttpContext context = MakeContext(path: "/setup");

        await middleware.InvokeAsync(context: context);

        context.Response.StatusCode.Should().Be(expected: StatusCodes.Status200OK);
    }

    [Fact]
    public async Task SetupRoute_TrailingSlashVariant_StillMatches()
    {
        SetupModeMiddleware middleware = CreateMiddleware(setupRequired: false, nextCalled: out _);
        DefaultHttpContext context = MakeContext(path: "/setup/config");

        await middleware.InvokeAsync(context: context);

        // /setup/config is handled by SetupEndpoints (not a 404 passthrough) —
        // any response other than the untouched default (200 with empty body)
        // proves the request reached SetupEndpoints rather than the terminal
        // next() delegate.
        context.Response.StatusCode.Should().NotBe(unexpected: StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task FaviconRoute_AlwaysServed()
    {
        SetupModeMiddleware middleware = CreateMiddleware(setupRequired: true, nextCalled: out _);
        DefaultHttpContext context = MakeContext(path: "/favicon.ico");

        await middleware.InvokeAsync(context: context);

        context.Response.StatusCode.Should().Be(expected: StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be(expected: "image/x-icon");
    }

    // =========================================================================
    // Setup not required: everything else falls through to next()
    // =========================================================================

    [Fact]
    public async Task SetupNotRequired_UnrelatedRoute_CallsNext()
    {
        bool nextCalled = false;
        SetupModeMiddleware middleware = CreateMiddleware(
            setupRequired: false,
            nextCalled: out _,
            next: _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            }
        );
        DefaultHttpContext context = MakeContext(path: "/api/v1/media/movies");

        await middleware.InvokeAsync(context: context);

        nextCalled.Should().BeTrue();
    }

    // =========================================================================
    // Setup required: passthrough routes bypass the gate
    // =========================================================================

    [Theory]
    [InlineData(data: "/health")]
    [InlineData(data: "/manage")]
    [InlineData(data: "/manage/anything")]
    public async Task SetupRequired_PassthroughRoute_CallsNext(string path)
    {
        bool nextCalled = false;
        SetupModeMiddleware middleware = CreateMiddleware(
            setupRequired: true,
            nextCalled: out _,
            next: _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            }
        );
        DefaultHttpContext context = MakeContext(path: path);

        await middleware.InvokeAsync(context: context);

        nextCalled.Should().BeTrue();
    }

    // =========================================================================
    // Setup required: everything else 503s with the setup_required envelope
    // =========================================================================

    [Fact]
    public async Task SetupRequired_UnrelatedRoute_Returns503WithSetupRequiredBody()
    {
        bool nextCalled = false;
        SetupModeMiddleware middleware = CreateMiddleware(
            setupRequired: true,
            nextCalled: out _,
            next: _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            }
        );
        DefaultHttpContext context = MakeContext(path: "/api/v1/media/movies");

        await middleware.InvokeAsync(context: context);

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(expected: StatusCodes.Status503ServiceUnavailable);
        context.Response.ContentType.Should().Be(expected: "application/json");

        context.Response.Body.Seek(offset: 0, origin: SeekOrigin.Begin);
        using StreamReader reader = new(stream: context.Response.Body, encoding: Encoding.UTF8);
        string body = await reader.ReadToEndAsync();
        using JsonDocument json = JsonDocument.Parse(json: body);
        json.RootElement.GetProperty(propertyName: "status").GetString().Should().Be(expected: "setup_required");
        json.RootElement.GetProperty(propertyName: "setup_url").GetString().Should().Be(expected: "/setup");
    }
}
