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

using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Api.Middleware;
using NoMercy.Authorization;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Information;
using Xunit;

namespace NoMercy.Tests.Api.Middleware;

// HubErrorLoggingFilter is the single choke point every SignalR hub method call
// passes through. Its job is: (1) let calls from connections it can't identify
// through untouched, (2) run identified calls under SQLite-retry protection,
// and (3) translate every failure mode a client can trigger (missing method,
// bad arguments, arbitrary exception) into a HubException whose message is
// verbose in dev and generic in production — never leak internals to a
// self-hosted user's browser console in prod, never hide them from the boss
// running --dev.
[Trait(name: "Category", value: "Middleware")]
public sealed class HubErrorLoggingFilterTests
{
    private sealed class FakeHub : Hub
    {
        public Task Ping() => Task.CompletedTask;
    }

    private static readonly MethodInfo PingMethod = typeof(FakeHub).GetMethod(
        name: nameof(FakeHub.Ping)
    )!;

    private static HubInvocationContext CreateInvocation(
        ClaimsPrincipal? user,
        IReadOnlyList<object?>? arguments = null
    )
    {
        Mock<HubCallerContext> context = new();
        context.Setup(expression: c => c.User).Returns(value: user);
        context.Setup(expression: c => c.ConnectionId).Returns(value: "test-connection");

        return new HubInvocationContext(
            context: context.Object,
            serviceProvider: Mock.Of<IServiceProvider>(),
            hub: new FakeHub(),
            hubMethod: PingMethod,
            hubMethodArguments: arguments ?? []
        );
    }

    private static ClaimsPrincipal PrincipalFor(Guid userId)
    {
        return new(identity: new ClaimsIdentity(claims: [new(type: ClaimTypes.NameIdentifier, value: userId.ToString())]));
    }

    private static HubErrorLoggingFilter CreateFilter()
    {
        return new(logger: NullLogger<HubErrorLoggingFilter>.Instance);
    }

    [Fact]
    public async Task InvokeMethodAsync_NullUser_PassesThroughWithoutTouchingUserCache()
    {
        HubErrorLoggingFilter filter = CreateFilter();
        HubInvocationContext invocation = CreateInvocation(user: null);

        object? result = await filter.InvokeMethodAsync(invocationContext: invocation, next: _ => new(result: "passthrough"));

        result.Should().Be(expected: "passthrough");
    }

    [Fact]
    public async Task InvokeMethodAsync_PrincipalWithoutNameIdentifier_PassesThrough()
    {
        HubErrorLoggingFilter filter = CreateFilter();
        ClaimsPrincipal claimless = new(identity: new ClaimsIdentity(claims: [new(type: "some-other-claim", value: "value")]));
        HubInvocationContext invocation = CreateInvocation(user: claimless);

        object? result = await filter.InvokeMethodAsync(invocationContext: invocation, next: _ => new(result: "passthrough"));

        result.Should().Be(expected: "passthrough");
    }

    [Fact]
    public async Task InvokeMethodAsync_MalformedGuidClaim_PassesThrough()
    {
        HubErrorLoggingFilter filter = CreateFilter();
        ClaimsPrincipal malformed = new(
            identity: new ClaimsIdentity(claims: [new(type: ClaimTypes.NameIdentifier, value: "not-a-guid")])
        );
        HubInvocationContext invocation = CreateInvocation(user: malformed);

        object? result = await filter.InvokeMethodAsync(invocationContext: invocation, next: _ => new(result: "passthrough"));

        result.Should().Be(expected: "passthrough");
    }

    [Fact]
    public async Task InvokeMethodAsync_ValidGuidNotInUserCache_PassesThrough()
    {
        HubErrorLoggingFilter filter = CreateFilter();
        // A freshly minted GUID is astronomically unlikely to already be a
        // seeded UserCache.Current entry left behind by another test class in
        // this sequential (DisableTestParallelization) assembly.
        HubInvocationContext invocation = CreateInvocation(user: PrincipalFor(userId: Guid.NewGuid()));

        object? result = await filter.InvokeMethodAsync(invocationContext: invocation, next: _ => new(result: "passthrough"));

        result.Should().Be(expected: "passthrough");
    }

    [Fact]
    public async Task InvokeMethodAsync_KnownUser_ReturnsInnerResult()
    {
        HubErrorLoggingFilter filter = CreateFilter();
        User user = new()
        {
            Id = Guid.NewGuid(),
            Name = "Filter Test User",
            Email = "filter-test@nomercy.tv",
        };
        UserCache.Current.AddUser(user: user);
        try
        {
            HubInvocationContext invocation = CreateInvocation(user: PrincipalFor(userId: user.Id));

            object? result = await filter.InvokeMethodAsync(invocationContext: invocation, next: _ => new(result: "ok"));

            result.Should().Be(expected: "ok");
        }
        finally
        {
            UserCache.Current.RemoveUser(user: user);
        }
    }

    [Fact]
    public async Task InvokeMethodAsync_KnownUser_HubExceptionFromNext_IsRethrownUnchanged()
    {
        HubErrorLoggingFilter filter = CreateFilter();
        User user = new()
        {
            Id = Guid.NewGuid(),
            Name = "Filter Test User",
            Email = "filter-test@nomercy.tv",
        };
        UserCache.Current.AddUser(user: user);
        try
        {
            HubInvocationContext invocation = CreateInvocation(user: PrincipalFor(userId: user.Id));

            Func<Task> act = async () =>
                await filter.InvokeMethodAsync(
                    invocationContext: invocation,
                    next: _ => throw new HubException(message: "client-facing failure")
                );

            (await act.Should().ThrowAsync<HubException>()).WithMessage(expectedWildcardPattern: "client-facing failure");
        }
        finally
        {
            UserCache.Current.RemoveUser(user: user);
        }
    }

    [Theory]
    [InlineData(data: true)]
    [InlineData(data: false)]
    public async Task InvokeMethodAsync_MethodDoesNotExist_MessageHonorsIsDev(bool isDev)
    {
        bool original = Config.IsDev;
        Config.IsDev = isDev;
        HubErrorLoggingFilter filter = CreateFilter();
        User user = new()
        {
            Id = Guid.NewGuid(),
            Name = "Filter Test User",
            Email = "filter-test@nomercy.tv",
        };
        UserCache.Current.AddUser(user: user);
        try
        {
            HubInvocationContext invocation = CreateInvocation(user: PrincipalFor(userId: user.Id));

            Func<Task> act = async () =>
                await filter.InvokeMethodAsync(
                    invocationContext: invocation,
                    next: _ => throw new InvalidOperationException(message: "Method 'Ping' does not exist.")
                );

            HubException thrown = (await act.Should().ThrowAsync<HubException>()).Which;
            if (isDev)
                thrown.Message.Should().Be(expected: "Method 'Ping' does not exist on hub 'FakeHub'");
            else
                thrown.Message.Should().Be(expected: "An internal error occurred");
        }
        finally
        {
            UserCache.Current.RemoveUser(user: user);
            Config.IsDev = original;
        }
    }

    [Fact]
    public async Task InvokeMethodAsync_UnrelatedInvalidOperationException_IsWrappedAsHubException()
    {
        // "does not exist" is a message substring match, not an exception
        // sub-type check -- any other InvalidOperationException must still be
        // caught by the final catch-all and wrapped, not bubble up raw.
        HubErrorLoggingFilter filter = CreateFilter();
        User user = new()
        {
            Id = Guid.NewGuid(),
            Name = "Filter Test User",
            Email = "filter-test@nomercy.tv",
        };
        UserCache.Current.AddUser(user: user);
        try
        {
            HubInvocationContext invocation = CreateInvocation(user: PrincipalFor(userId: user.Id));

            Func<Task> act = async () =>
                await filter.InvokeMethodAsync(
                    invocationContext: invocation,
                    next: _ => throw new InvalidOperationException(message: "unrelated failure")
                );

            await act.Should().ThrowAsync<HubException>();
        }
        finally
        {
            UserCache.Current.RemoveUser(user: user);
        }
    }

    [Theory]
    [InlineData(data: true)]
    [InlineData(data: false)]
    public async Task InvokeMethodAsync_ArgumentException_MessageHonorsIsDev(bool isDev)
    {
        bool original = Config.IsDev;
        Config.IsDev = isDev;
        HubErrorLoggingFilter filter = CreateFilter();
        User user = new()
        {
            Id = Guid.NewGuid(),
            Name = "Filter Test User",
            Email = "filter-test@nomercy.tv",
        };
        UserCache.Current.AddUser(user: user);
        try
        {
            HubInvocationContext invocation = CreateInvocation(
                user: PrincipalFor(userId: user.Id),
                arguments: ["wrong-type"]
            );

            Func<Task> act = async () =>
                await filter.InvokeMethodAsync(
                    invocationContext: invocation,
                    next: _ => throw new ArgumentException(message: "expected int, got string")
                );

            HubException thrown = (await act.Should().ThrowAsync<HubException>()).Which;
            if (isDev)
                thrown
                    .Message.Should()
                    .Be(expected: "Invalid arguments for method 'Ping': expected int, got string");
            else
                thrown.Message.Should().Be(expected: "An internal error occurred");
        }
        finally
        {
            UserCache.Current.RemoveUser(user: user);
            Config.IsDev = original;
        }
    }

    [Fact]
    public async Task InvokeMethodAsync_ArgumentException_WithNoArguments_StillWraps()
    {
        HubErrorLoggingFilter filter = CreateFilter();
        User user = new()
        {
            Id = Guid.NewGuid(),
            Name = "Filter Test User",
            Email = "filter-test@nomercy.tv",
        };
        UserCache.Current.AddUser(user: user);
        try
        {
            HubInvocationContext invocation = CreateInvocation(
                user: PrincipalFor(userId: user.Id),
                arguments: []
            );

            Func<Task> act = async () =>
                await filter.InvokeMethodAsync(
                    invocationContext: invocation,
                    next: _ => throw new ArgumentException(message: "missing required argument")
                );

            await act.Should().ThrowAsync<HubException>();
        }
        finally
        {
            UserCache.Current.RemoveUser(user: user);
        }
    }

    [Theory]
    [InlineData(data: true)]
    [InlineData(data: false)]
    public async Task InvokeMethodAsync_UnhandledException_MessageHonorsIsDev(bool isDev)
    {
        bool original = Config.IsDev;
        Config.IsDev = isDev;
        HubErrorLoggingFilter filter = CreateFilter();
        User user = new()
        {
            Id = Guid.NewGuid(),
            Name = "Filter Test User",
            Email = "filter-test@nomercy.tv",
        };
        UserCache.Current.AddUser(user: user);
        try
        {
            HubInvocationContext invocation = CreateInvocation(
                user: PrincipalFor(userId: user.Id),
                arguments: [1, "two"]
            );

            Func<Task> act = async () =>
                await filter.InvokeMethodAsync(
                    invocationContext: invocation,
                    next: _ => throw new InvalidCastException(message: "boom")
                );

            HubException thrown = (await act.Should().ThrowAsync<HubException>()).Which;
            if (isDev)
                thrown.Message.Should().Be(expected: "An error occurred calling 'Ping': boom");
            else
                thrown.Message.Should().Be(expected: "An internal error occurred");
        }
        finally
        {
            UserCache.Current.RemoveUser(user: user);
            Config.IsDev = original;
        }
    }
}
