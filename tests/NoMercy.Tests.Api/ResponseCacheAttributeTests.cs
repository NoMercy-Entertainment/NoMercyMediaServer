using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.Controllers.V1;
using NoMercy.Api.Controllers.V1.Dashboard;
using NoMercy.Api.Controllers.V1.Media;
using MediaLibrariesController = NoMercy.Api.Controllers.V1.Media.LibrariesController;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait("Category", "Unit")]
public class ResponseCacheAttributeTests
{
    [Theory]
    [InlineData(typeof(GenresController), nameof(GenresController.Genres), 300)]
    [InlineData(typeof(GenresController), nameof(GenresController.Genre), 300)]
    [InlineData(typeof(PeopleController), "Index", 300)]
    [InlineData(typeof(PeopleController), "Show", 300)]
    [InlineData(typeof(CollectionsController), "Collections", 300)]
    [InlineData(typeof(CollectionsController), "Collection", 300)]
    [InlineData(typeof(MediaLibrariesController), "Libraries", 300)]
    [InlineData(typeof(MoviesController), "Movie", 120)]
    [InlineData(typeof(TvShowsController), "Tv", 120)]
    [InlineData(typeof(ConfigurationController), "Languages", 3600)]
    [InlineData(typeof(ConfigurationController), "Countries", 3600)]
    [InlineData(typeof(ServerController), "ServerPaths", 3600)]
    [InlineData(typeof(SetupController), "Status", 30)]
    public void CacheableEndpoint_HasResponseCacheAttribute_WithCorrectDuration(
        Type controllerType, string methodName, int expectedDuration)
    {
        MethodInfo? method = controllerType.GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);

        ResponseCacheAttribute? attr = method.GetCustomAttribute<ResponseCacheAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(expectedDuration, attr.Duration);
        Assert.False(attr.NoStore, $"{controllerType.Name}.{methodName} should not have NoStore=true");
    }

    [Theory]
    [InlineData(typeof(UserDataController), "ContinueWatching")]
    [InlineData(typeof(HomeController), "Home")]
    [InlineData(typeof(SearchController), "SearchMusic")]
    [InlineData(typeof(SearchController), "SearchVideo")]
    [InlineData(typeof(ServerController), "Resources")]
    [InlineData(typeof(ServerController), "ServerInfo")]
    [InlineData(typeof(SetupController), "ServerInfo")]
    public void RealTimeEndpoint_HasResponseCacheNoStore(Type controllerType, string methodName)
    {
        MethodInfo? method = controllerType.GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);

        ResponseCacheAttribute? attr = method.GetCustomAttribute<ResponseCacheAttribute>();
        Assert.NotNull(attr);
        Assert.True(attr.NoStore, $"{controllerType.Name}.{methodName} should have NoStore=true");
    }

    [Theory]
    [InlineData(typeof(GenresController), nameof(GenresController.Genres), new[] { "take", "page" })]
    [InlineData(typeof(GenresController), nameof(GenresController.Genre), new[] { "take", "page", "version" })]
    [InlineData(typeof(CollectionsController), "Collections", new[] { "take", "page", "version" })]
    public void CacheableEndpoint_VariesByQueryKeys(
        Type controllerType, string methodName, string[] expectedKeys)
    {
        MethodInfo? method = controllerType.GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);

        ResponseCacheAttribute? attr = method.GetCustomAttribute<ResponseCacheAttribute>();
        Assert.NotNull(attr);
        Assert.NotNull(attr.VaryByQueryKeys);
        Assert.Equal(expectedKeys.OrderBy(k => k), attr.VaryByQueryKeys.OrderBy(k => k));
    }
}
