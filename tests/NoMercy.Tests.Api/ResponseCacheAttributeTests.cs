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
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.Controllers.V1;
using NoMercy.Api.Controllers.V1.Dashboard.Admin;
using NoMercy.Api.Controllers.V1.Media;
using Xunit;
using MediaLibrariesController = NoMercy.Api.Controllers.V1.Media.LibrariesController;

namespace NoMercy.Tests.Api;

[Trait(name: "Category", value: "Unit")]
public class ResponseCacheAttributeTests
{
    [Theory]
    [InlineData(data: [typeof(GenresController), nameof(GenresController.Genres), 300])]
    [InlineData(data: [typeof(GenresController), nameof(GenresController.Genre), 300])]
    [InlineData(data: [typeof(PeopleController), "Index", 300])]
    [InlineData(data: [typeof(PeopleController), "Show", 300])]
    [InlineData(data: [typeof(CollectionsController), "Collections", 300])]
    [InlineData(data: [typeof(CollectionsController), "Collection", 300])]
    [InlineData(data: [typeof(MediaLibrariesController), "Libraries", 300])]
    [InlineData(data: [typeof(MoviesController), "Movie", 120])]
    [InlineData(data: [typeof(TvShowsController), "Tv", 120])]
    [InlineData(data: [typeof(ConfigurationController), "Languages", 3600])]
    [InlineData(data: [typeof(ConfigurationController), "Countries", 3600])]
    [InlineData(data: [typeof(ServerController), "ServerPaths", 3600])]
    [InlineData(data: [typeof(SetupController), "Status", 30])]
    public void CacheableEndpoint_HasResponseCacheAttribute_WithCorrectDuration(
        Type controllerType,
        string methodName,
        int expectedDuration
    )
    {
        MethodInfo? method = controllerType.GetMethod(
            name: methodName,
            bindingAttr: BindingFlags.Instance | BindingFlags.Public
        );
        Assert.NotNull(@object: method);

        ResponseCacheAttribute? attr = method.GetCustomAttribute<ResponseCacheAttribute>();
        Assert.NotNull(@object: attr);
        Assert.Equal(expected: expectedDuration, actual: attr.Duration);
        Assert.False(
            condition: attr.NoStore,
            userMessage: $"{controllerType.Name}.{methodName} should not have NoStore=true"
        );
    }

    [Theory]
    [InlineData(data: [typeof(UserDataController), "ContinueWatching"])]
    [InlineData(data: [typeof(HomeController), "Home"])]
    [InlineData(data: [typeof(SearchController), "SearchMusic"])]
    [InlineData(data: [typeof(SearchController), "SearchVideo"])]
    [InlineData(data: [typeof(ServerController), "Resources"])]
    [InlineData(data: [typeof(ServerController), "ServerInfo"])]
    [InlineData(data: [typeof(SetupController), "ServerInfo"])]
    public void RealTimeEndpoint_HasResponseCacheNoStore(Type controllerType, string methodName)
    {
        MethodInfo? method = controllerType.GetMethod(
            name: methodName,
            bindingAttr: BindingFlags.Instance | BindingFlags.Public
        );
        Assert.NotNull(@object: method);

        ResponseCacheAttribute? attr = method.GetCustomAttribute<ResponseCacheAttribute>();
        Assert.NotNull(@object: attr);
        Assert.True(condition: attr.NoStore, userMessage: $"{controllerType.Name}.{methodName} should have NoStore=true");
    }

    [Theory]
    [InlineData(data: [typeof(GenresController), nameof(GenresController.Genres), new[] { "take", "page" }])]
    [InlineData(data: [typeof(GenresController), nameof(GenresController.Genre), new[] { "take", "page", "version" }])]
    [InlineData(data: [typeof(CollectionsController), "Collections", new[] { "take", "page", "version" }])]
    public void CacheableEndpoint_VariesByQueryKeys(
        Type controllerType,
        string methodName,
        string[] expectedKeys
    )
    {
        MethodInfo? method = controllerType.GetMethod(
            name: methodName,
            bindingAttr: BindingFlags.Instance | BindingFlags.Public
        );
        Assert.NotNull(@object: method);

        ResponseCacheAttribute? attr = method.GetCustomAttribute<ResponseCacheAttribute>();
        Assert.NotNull(@object: attr);
        Assert.NotNull(@object: attr.VaryByQueryKeys);
        Assert.Equal(expected: expectedKeys.OrderBy(keySelector: k => k), actual: attr.VaryByQueryKeys.OrderBy(keySelector: k => k));
    }
}
