using NoMercy.Plugin.Samples.Dashboard;
using NoMercy.Plugins.Abstractions;
using Xunit;

namespace NoMercy.Tests.Api.NmComponents;

public class PluginNavigationTests
{
    private static readonly Guid Id = Guid.Parse("66666666-7777-8888-9999-000000000000");
    private static readonly DashboardSamplePlugin Plugin = new();

    [Fact]
    public void ResolvesAPluginRouteAgainstWhereverThePluginLives()
    {
        // The plugin wrote "/details/42" and nothing else. The same tree works
        // under music and under addons because it never wrote either down.
        PluginActionIntent intent = PluginNavigation.To("/details/42");

        Assert.Equal($"/music/plugins/{Id}/details/42", PluginNavigation.Resolve(intent, PluginKind.Music, Id));
        Assert.Equal($"/addons/{Id}/details/42", PluginNavigation.Resolve(intent, PluginKind.Addon, Id));
    }

    [Fact]
    public void SendsAPluginBackToItsOwnRoot()
    {
        Assert.Equal(
            $"/music/plugins/{Id}",
            PluginNavigation.Resolve(PluginNavigation.To("/"), PluginKind.Music, Id));
    }

    [Fact]
    public void LeavesAnAppPathAlone()
    {
        // Navigating out of its own space is the exception, and the only case
        // where a plugin is allowed to name a path the app owns.
        Assert.Equal(
            "/libraries/music",
            PluginNavigation.Resolve(PluginNavigation.ToApp("/libraries/music"), PluginKind.Music, Id));
    }

    [Fact]
    public void RefusesARouteThatIsNotOne()
    {
        // "details/42" would resolve against the prefix without a separator and
        // silently point at a sibling of the plugin.
        Assert.Throws<ArgumentException>(() => PluginNavigation.To("details/42"));
        Assert.Throws<ArgumentException>(() => PluginNavigation.ToApp("libraries"));
    }

    [Fact]
    public async Task ServesANestedPageOfItsOwn()
    {
        PluginView view = await Plugin.GetViewAsync(
            new() { Route = "/details/42", Surface = PluginSurface.Web }, CancellationToken.None);

        Assert.Contains(view.Components ?? [], component => component.Id == "detail");
    }

    [Fact]
    public async Task FallsBackToItsRootForARouteItDoesNotKnow()
    {
        // A viewer following a stale link should land on the plugin rather than
        // on an empty page that looks like the plugin is broken.
        PluginView view = await Plugin.GetViewAsync(
            new() { Route = "/gone", Surface = PluginSurface.Web }, CancellationToken.None);

        Assert.Contains(view.Components ?? [], component => component.Id == "recent");
    }

    [Fact]
    public void OffersAFormOnlyWhereItCanBeFilledIn()
    {
        PluginNavEntry settings = Plugin.NavEntries.First(entry => entry.Route == "/settings");

        Assert.True(settings.AppearsOn(PluginSurface.Web));
        Assert.False(settings.AppearsOn(PluginSurface.Tv));
    }

    [Fact]
    public void OffersAnEntryEverywhereWhenItNamesNoSurfaces()
    {
        // Saying nothing means everywhere. The opposite default would hide a
        // screen its author never thought about.
        PluginNavEntry browse = Plugin.NavEntries.First(entry => entry.Route == "/");

        foreach (string surface in PluginSurface.All)
            Assert.True(browse.AppearsOn(surface));
    }

    [Fact]
    public void PlacesItsTwoScreensInDifferentAreas()
    {
        // The reason kind is on the mount: one plugin, a browse page with the
        // music and its settings in the dashboard.
        Assert.Equal(PluginKind.Music, Plugin.NavEntries.First(entry => entry.Route == "/").Section);
        Assert.Equal(PluginKind.Dashboard, Plugin.NavEntries.First(entry => entry.Route == "/settings").Section);
    }
}
