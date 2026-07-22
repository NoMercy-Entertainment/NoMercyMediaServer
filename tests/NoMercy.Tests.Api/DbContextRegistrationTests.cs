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

using Microsoft.Extensions.DependencyInjection;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait(name: "Category", value: "Characterization")]
public class DbContextRegistrationTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public DbContextRegistrationTests(NoMercyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void MediaContext_ResolvedOncePerScope_ReturnsSameInstance()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        MediaContext first = scope.ServiceProvider.GetRequiredService<MediaContext>();
        MediaContext second = scope.ServiceProvider.GetRequiredService<MediaContext>();

        Assert.Same(expected: first, actual: second);
    }

    [Fact]
    public void QueueContext_ResolvedOncePerScope_ReturnsSameInstance()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        QueueContext first = scope.ServiceProvider.GetRequiredService<QueueContext>();
        QueueContext second = scope.ServiceProvider.GetRequiredService<QueueContext>();

        Assert.Same(expected: first, actual: second);
    }

    [Fact]
    public void MediaContext_DifferentScopes_ReturnDifferentInstances()
    {
        using IServiceScope scope1 = _factory.Services.CreateScope();
        using IServiceScope scope2 = _factory.Services.CreateScope();

        MediaContext ctx1 = scope1.ServiceProvider.GetRequiredService<MediaContext>();
        MediaContext ctx2 = scope2.ServiceProvider.GetRequiredService<MediaContext>();

        Assert.NotSame(expected: ctx1, actual: ctx2);
    }

    [Fact]
    public void QueueContext_DifferentScopes_ReturnDifferentInstances()
    {
        using IServiceScope scope1 = _factory.Services.CreateScope();
        using IServiceScope scope2 = _factory.Services.CreateScope();

        QueueContext ctx1 = scope1.ServiceProvider.GetRequiredService<QueueContext>();
        QueueContext ctx2 = scope2.ServiceProvider.GetRequiredService<QueueContext>();

        Assert.NotSame(expected: ctx1, actual: ctx2);
    }

    [Fact]
    public void MediaContext_ScopedRegistration_NotTransient()
    {
        // Verify that the registration is Scoped, not Transient.
        // With Transient, each GetRequiredService call returns a new instance.
        // With Scoped, both calls within the same scope return the same instance.
        using IServiceScope scope = _factory.Services.CreateScope();
        MediaContext first = scope.ServiceProvider.GetRequiredService<MediaContext>();
        MediaContext second = scope.ServiceProvider.GetRequiredService<MediaContext>();

        // If this were transient, ReferenceEquals would be false
        Assert.True(
            condition: ReferenceEquals(objA: first, objB: second),
            userMessage: "MediaContext should be scoped (same instance per scope), not transient (new instance per resolution)"
        );
    }

    [Fact]
    public void QueueContext_ScopedRegistration_NotTransient()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        QueueContext first = scope.ServiceProvider.GetRequiredService<QueueContext>();
        QueueContext second = scope.ServiceProvider.GetRequiredService<QueueContext>();

        Assert.True(
            condition: ReferenceEquals(objA: first, objB: second),
            userMessage: "QueueContext should be scoped (same instance per scope), not transient (new instance per resolution)"
        );
    }

    [Fact]
    public void MediaContext_SaveChanges_PersistsWithinScope()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        MediaContext context = scope.ServiceProvider.GetRequiredService<MediaContext>();

        // Track a change on the scoped context
        User? user = context.Users.FirstOrDefault();
        Assert.NotNull(@object: user);

        string originalName = user.Name;
        string tempName = $"Test_{Guid.NewGuid():N}";
        user.Name = tempName;
        context.SaveChanges();

        // Re-resolve from same scope — should be same instance with same change tracker
        MediaContext sameContext = scope.ServiceProvider.GetRequiredService<MediaContext>();
        User? reloaded = sameContext.Users.FirstOrDefault(predicate: u => u.Id == user.Id);
        Assert.NotNull(@object: reloaded);
        Assert.Equal(expected: tempName, actual: reloaded.Name);

        // Restore original name
        reloaded.Name = originalName;
        sameContext.SaveChanges();
    }

    [Fact]
    public void MediaContext_ChangeTracking_SharedAcrossResolutionsInScope()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        MediaContext ctx1 = scope.ServiceProvider.GetRequiredService<MediaContext>();
        MediaContext ctx2 = scope.ServiceProvider.GetRequiredService<MediaContext>();

        // Since they're the same instance, changes tracked by ctx1 are visible to ctx2
        User? user = ctx1.Users.FirstOrDefault();
        Assert.NotNull(@object: user);

        string originalName = user.Name;
        user.Name = "SharedTracking";

        // ctx2 should see the same entity with the modified name (same change tracker)
        User? fromCtx2 = ctx2.Users.Local.FirstOrDefault(predicate: u => u.Id == user.Id);
        Assert.NotNull(@object: fromCtx2);
        Assert.Equal(expected: "SharedTracking", actual: fromCtx2.Name);

        // Restore
        user.Name = originalName;
    }
}
