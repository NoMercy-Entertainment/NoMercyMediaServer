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
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Api.Middleware;
using NoMercy.Api.Services;
using NoMercy.Authorization;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Storage;
using NoMercy.Database.Models.Users;
using NoMercy.Service.Authorization;
using Xunit;

namespace NoMercy.Tests.Api.Authorization;

[Trait(name: "Category", value: "Authorization")]
public sealed class AuthorizationDenyPrecisionTests
{
    private static readonly Guid OwnerId = Guid.Parse(input: "aa000000-0000-0000-0000-000000000001");
    private static readonly Guid ModeratorId = Guid.Parse(input: "bb000000-0000-0000-0000-000000000002");
    private static readonly Guid AllowedId = Guid.Parse(input: "cc000000-0000-0000-0000-000000000003");
    private static readonly Guid BlockedId = Guid.Parse(input: "dd000000-0000-0000-0000-000000000004");

    private static User OwnerUser =>
        new()
        {
            Id = OwnerId,
            Owner = true,
            Manage = false,
            Allowed = false,
            Name = "Owner",
            Email = "owner@nm.tv",
        };

    private static User ModeratorUser =>
        new()
        {
            Id = ModeratorId,
            Owner = false,
            Manage = true,
            Allowed = true,
            Name = "Mod",
            Email = "mod@nm.tv",
        };

    private static User AllowedUser =>
        new()
        {
            Id = AllowedId,
            Owner = false,
            Manage = false,
            Allowed = true,
            Name = "Allowed",
            Email = "allowed@nm.tv",
        };

    private static User BlockedUser =>
        new()
        {
            Id = BlockedId,
            Owner = false,
            Manage = false,
            Allowed = false,
            Name = "Blocked",
            Email = "blocked@nm.tv",
        };

    private static ClaimsPrincipal PrincipalFor(Guid userId)
    {
        List<Claim> claims = [new(type: ClaimTypes.NameIdentifier, value: userId.ToString())];
        return new(identity: new ClaimsIdentity(claims: claims, authenticationType: "TestScheme"));
    }

    private static (MediaAuthorizationHandler handler, UserCache cache) BuildHandler()
    {
        UserCache cache = new();
        MediaAuthorizationPolicy policy = new(userCache: cache);
        MediaAuthorizationHandler handler = new(
            policy: policy,
            logger: NullLogger<MediaAuthorizationHandler>.Instance
        );
        return (handler, cache);
    }

    private static AuthorizationHandlerContext HandlerContext(
        ClaimsPrincipal user,
        IAuthorizationRequirement requirement
    ) => new(requirements: [requirement], user: user, resource: null);

    private static IAuthorizationService BuildNamedPolicyService(UserCache cache)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IUserCache>(implementationInstance: cache);
        services.AddSingleton<IMediaAuthorizationPolicy, MediaAuthorizationPolicy>();
        services.AddScoped<IAuthorizationHandler, MediaAuthorizationHandler>();
        services
            .AddAuthorizationBuilder()
            .AddPolicy(
                name: "Owner",
                configurePolicy: policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.AddRequirements(requirements: new OwnerRequirement());
                }
            )
            .AddPolicy(
                name: "Moderator",
                configurePolicy: policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.AddRequirements(requirements: new ModeratorRequirement());
                }
            )
            .AddPolicy(
                name: "MediaAccess",
                configurePolicy: policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.AddRequirements(requirements: new MediaAccessRequirement());
                }
            );
        ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IAuthorizationService>();
    }

    [Fact]
    public async Task OwnerRequirement_Denies_WhenPrincipalIsModeratorNotOwner()
    {
        (MediaAuthorizationHandler handler, UserCache cache) = BuildHandler();
        cache.AddUser(user: OwnerUser);
        cache.AddUser(user: ModeratorUser);
        AuthorizationHandlerContext ctx = HandlerContext(
            user: PrincipalFor(userId: ModeratorId),
            requirement: new OwnerRequirement()
        );

        await handler.HandleAsync(context: ctx);

        ctx.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task OwnerRequirement_Admits_WhenPrincipalIsOwnerRegardlessOfManageFlag()
    {
        (MediaAuthorizationHandler handler, UserCache cache) = BuildHandler();
        cache.AddUser(user: OwnerUser);
        AuthorizationHandlerContext ctx = HandlerContext(
            user: PrincipalFor(userId: OwnerId),
            requirement: new OwnerRequirement()
        );

        await handler.HandleAsync(context: ctx);

        ctx.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task ModeratorRequirement_Admits_WhenPrincipalIsOwnerWithoutManageFlag()
    {
        (MediaAuthorizationHandler handler, UserCache cache) = BuildHandler();
        cache.AddUser(user: OwnerUser);
        AuthorizationHandlerContext ctx = HandlerContext(
            user: PrincipalFor(userId: OwnerId),
            requirement: new ModeratorRequirement()
        );

        await handler.HandleAsync(context: ctx);

        ctx.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task ModeratorRequirement_Denies_WhenPrincipalHasAllowedButNotManageOrOwner()
    {
        (MediaAuthorizationHandler handler, UserCache cache) = BuildHandler();
        cache.AddUser(user: AllowedUser);
        AuthorizationHandlerContext ctx = HandlerContext(
            user: PrincipalFor(userId: AllowedId),
            requirement: new ModeratorRequirement()
        );

        await handler.HandleAsync(context: ctx);

        ctx.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task MediaAccessRequirement_Admits_WhenPrincipalIsOwnerWithoutAllowedFlag()
    {
        (MediaAuthorizationHandler handler, UserCache cache) = BuildHandler();
        cache.AddUser(user: OwnerUser);
        AuthorizationHandlerContext ctx = HandlerContext(
            user: PrincipalFor(userId: OwnerId),
            requirement: new MediaAccessRequirement()
        );

        await handler.HandleAsync(context: ctx);

        ctx.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task MediaAccessRequirement_Denies_WhenPrincipalIsInCacheButAllowedFalse()
    {
        (MediaAuthorizationHandler handler, UserCache cache) = BuildHandler();
        cache.AddUser(user: BlockedUser);
        AuthorizationHandlerContext ctx = HandlerContext(
            user: PrincipalFor(userId: BlockedId),
            requirement: new MediaAccessRequirement()
        );

        await handler.HandleAsync(context: ctx);

        ctx.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task OwnerPolicy_Denies_WhenPrincipalIsModeratorNotOwner()
    {
        UserCache cache = new();
        cache.AddUser(user: OwnerUser);
        cache.AddUser(user: ModeratorUser);
        IAuthorizationService authService = BuildNamedPolicyService(cache: cache);

        AuthorizationResult result = await authService.AuthorizeAsync(
            user: PrincipalFor(userId: ModeratorId),
            policyName: "Owner"
        );

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task OwnerPolicy_Admits_WhenPrincipalIsOwner()
    {
        UserCache cache = new();
        cache.AddUser(user: OwnerUser);
        IAuthorizationService authService = BuildNamedPolicyService(cache: cache);

        AuthorizationResult result = await authService.AuthorizeAsync(
            user: PrincipalFor(userId: OwnerId),
            policyName: "Owner"
        );

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task ModeratorPolicy_Denies_WhenPrincipalHasAllowedButNotManageOrOwner()
    {
        UserCache cache = new();
        cache.AddUser(user: AllowedUser);
        IAuthorizationService authService = BuildNamedPolicyService(cache: cache);

        AuthorizationResult result = await authService.AuthorizeAsync(
            user: PrincipalFor(userId: AllowedId),
            policyName: "Moderator"
        );

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task ModeratorPolicy_Admits_WhenPrincipalIsModeratorWithManageFlag()
    {
        UserCache cache = new();
        cache.AddUser(user: ModeratorUser);
        IAuthorizationService authService = BuildNamedPolicyService(cache: cache);

        AuthorizationResult result = await authService.AuthorizeAsync(
            user: PrincipalFor(userId: ModeratorId),
            policyName: "Moderator"
        );

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task ModeratorPolicy_Admits_WhenPrincipalIsOwnerWithoutManageFlag()
    {
        UserCache cache = new();
        cache.AddUser(user: OwnerUser);
        IAuthorizationService authService = BuildNamedPolicyService(cache: cache);

        AuthorizationResult result = await authService.AuthorizeAsync(
            user: PrincipalFor(userId: OwnerId),
            policyName: "Moderator"
        );

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task MediaAccessPolicy_Denies_WhenPrincipalIsInCacheButAllowedFalse()
    {
        UserCache cache = new();
        cache.AddUser(user: BlockedUser);
        IAuthorizationService authService = BuildNamedPolicyService(cache: cache);

        AuthorizationResult result = await authService.AuthorizeAsync(
            user: PrincipalFor(userId: BlockedId),
            policyName: "MediaAccess"
        );

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task MediaAccessPolicy_Admits_WhenPrincipalHasAllowedFlag()
    {
        UserCache cache = new();
        cache.AddUser(user: AllowedUser);
        IAuthorizationService authService = BuildNamedPolicyService(cache: cache);

        AuthorizationResult result = await authService.AuthorizeAsync(
            user: PrincipalFor(userId: AllowedId),
            policyName: "MediaAccess"
        );

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task MediaAccessPolicy_Admits_WhenPrincipalIsOwnerWithoutAllowedFlag()
    {
        UserCache cache = new();
        cache.AddUser(user: OwnerUser);
        IAuthorizationService authService = BuildNamedPolicyService(cache: cache);

        AuthorizationResult result = await authService.AuthorizeAsync(
            user: PrincipalFor(userId: OwnerId),
            policyName: "MediaAccess"
        );

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task IsOwner_Denies_WhenPrincipalIdMatchesNonOwnerInMultiUserCache()
    {
        UserCache cache = new();
        cache.AddUser(user: OwnerUser);
        cache.AddUser(user: ModeratorUser);
        MediaAuthorizationPolicy policy = new(userCache: cache);

        bool result = policy.IsOwner(principal: PrincipalFor(userId: ModeratorId));

        result.Should().BeFalse();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task IsOwner_Admits_WhenPrincipalIdMatchesOwnerInMultiUserCache()
    {
        UserCache cache = new();
        cache.AddUser(user: ModeratorUser);
        cache.AddUser(user: AllowedUser);
        cache.AddUser(user: OwnerUser);
        MediaAuthorizationPolicy policy = new(userCache: cache);

        bool result = policy.IsOwner(principal: PrincipalFor(userId: OwnerId));

        result.Should().BeTrue();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Policy_SeesUserAddedAfterConstruction()
    {
        UserCache cache = new();
        MediaAuthorizationPolicy policy = new(userCache: cache);

        bool beforeAdd = policy.IsAllowed(principal: PrincipalFor(userId: AllowedId));
        cache.AddUser(user: AllowedUser);
        bool afterAdd = policy.IsAllowed(principal: PrincipalFor(userId: AllowedId));

        beforeAdd.Should().BeFalse();
        afterAdd.Should().BeTrue();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Policy_DeniesAfterAllowedFlagRevokedViaUpdateUser()
    {
        UserCache cache = new();
        cache.AddUser(user: AllowedUser);
        MediaAuthorizationPolicy policy = new(userCache: cache);

        bool beforeRevoke = policy.IsAllowed(principal: PrincipalFor(userId: AllowedId));

        User revoked = new()
        {
            Id = AllowedId,
            Owner = false,
            Manage = false,
            Allowed = false,
            Name = "Allowed",
            Email = "allowed@nm.tv",
        };
        cache.UpdateUser(user: revoked);

        bool afterRevoke = policy.IsAllowed(principal: PrincipalFor(userId: AllowedId));

        beforeRevoke.Should().BeTrue();
        afterRevoke.Should().BeFalse();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Policy_DeniesAfterUserRemovedFromCache()
    {
        UserCache cache = new();
        cache.AddUser(user: AllowedUser);
        MediaAuthorizationPolicy policy = new(userCache: cache);

        bool beforeRemove = policy.IsAllowed(principal: PrincipalFor(userId: AllowedId));
        cache.RemoveUser(user: AllowedUser);
        bool afterRemove = policy.IsAllowed(principal: PrincipalFor(userId: AllowedId));

        beforeRemove.Should().BeTrue();
        afterRemove.Should().BeFalse();
        await Task.CompletedTask;
    }

    [Fact]
    public void CatalogueCompleteness_AllThreeRequirementsHaveFiresOnBadAndSilentOnValidNeighborCoverage()
    {
        Type[] requirements =
        [
            typeof(OwnerRequirement),
            typeof(ModeratorRequirement),
            typeof(MediaAccessRequirement),
        ];

        IReadOnlyList<string> documentedCoverage =
        [
            nameof(OwnerRequirement_Denies_WhenPrincipalIsModeratorNotOwner),
            nameof(OwnerRequirement_Admits_WhenPrincipalIsOwnerRegardlessOfManageFlag),
            nameof(ModeratorRequirement_Denies_WhenPrincipalHasAllowedButNotManageOrOwner),
            nameof(ModeratorRequirement_Admits_WhenPrincipalIsOwnerWithoutManageFlag),
            nameof(MediaAccessRequirement_Denies_WhenPrincipalIsInCacheButAllowedFalse),
            nameof(MediaAccessRequirement_Admits_WhenPrincipalIsOwnerWithoutAllowedFlag),
        ];

        requirements
            .Should()
            .AllSatisfy(expected: req =>
            {
                string reqName = req.Name;
                IEnumerable<string> firesOnBad = documentedCoverage.Where(predicate: n =>
                    n.StartsWith(value: reqName, comparisonType: StringComparison.Ordinal) && n.Contains(value: "Denies")
                );
                IEnumerable<string> silentOnValid = documentedCoverage.Where(predicate: n =>
                    n.StartsWith(value: reqName, comparisonType: StringComparison.Ordinal) && n.Contains(value: "Admits")
                );

                firesOnBad
                    .Should()
                    .NotBeEmpty(
                        because: $"{reqName} must have at least one fires-on-bad deny test"
                    );
                silentOnValid
                    .Should()
                    .NotBeEmpty(
                        because: $"{reqName} must have at least one silent-on-valid-neighbor admit test"
                    );
            });
    }
}

[Trait(name: "Category", value: "Authorization")]
public sealed class TokenParamAuthDenyPrecisionTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MediaContext> _dbOptions;

    private static readonly Ulid KnownFolderId = Ulid.NewUlid();
    private static readonly Guid KnownUserId = Guid.NewGuid();

    public TokenParamAuthDenyPrecisionTests()
    {
        _connection = new(connectionString: $"DataSource={Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        _connection.Open();

        _dbOptions = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(connection: _connection)
            .AddInterceptors(interceptors: new SqliteNormalizeSearchInterceptor())
            .Options;

        using MediaContext ctx = new(options: _dbOptions);
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
        ctx.Drivers.Add(entity: driver);

        Folder folder = new()
        {
            Id = KnownFolderId,
            Path = "/media",
            DriverId = Driver.SystemLocalDriverId,
        };
        ctx.Folders.Add(entity: folder);

        User user = new()
        {
            Id = KnownUserId,
            Name = "Known",
            Email = "k@nm.tv",
            Allowed = true,
        };
        ctx.Users.Add(entity: user);
        ctx.SaveChanges();
    }

    public async Task InitializeAsync()
    {
        UserCache.Current.Reset();
        await using MediaContext ctx = new(options: _dbOptions);
        await UserCache.Current.InitializeAsync(context: ctx);
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

    private static TokenParamAuthMiddleware BuildMiddleware(RequestDelegate next) =>
        new(next: next, ingestKeyStore: new LiveIngestKeyStore(), logger: NullLogger<TokenParamAuthMiddleware>.Instance);

    private static HttpContext BuildContext(string path, ClaimsPrincipal? user = null)
    {
        DefaultHttpContext context = new();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        if (user is not null)
            context.User = user;
        return context;
    }

    private static ClaimsPrincipal PrincipalWithSub(string sub)
    {
        List<Claim> claims = [new(type: ClaimTypes.NameIdentifier, value: sub)];
        return new(identity: new ClaimsIdentity(claims: claims, authenticationType: "TestScheme"));
    }

    [Fact]
    public async Task Denies_WithForbidden_WhenSubIsGuidEmptyString()
    {
        bool nextCalled = false;
        TokenParamAuthMiddleware middleware = BuildMiddleware(next: _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        HttpContext context = BuildContext(
            path: $"/{KnownFolderId}/some-file.mkv",
            user: PrincipalWithSub(sub: Guid.Empty.ToString())
        );

        await middleware.InvokeAsync(context: context);

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(expected: (int)HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Allows_WhenFolderIdsEmptyAndPathLooksLikeUlid()
    {
        UserCache.Current.Reset();
        bool nextCalled = false;
        TokenParamAuthMiddleware middleware = BuildMiddleware(next: _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        Ulid ulidLookingPath = Ulid.NewUlid();
        HttpContext context = BuildContext(path: $"/{ulidLookingPath}/some-file.mkv");

        await middleware.InvokeAsync(context: context);

        nextCalled.Should().BeTrue();
    }
}
