using NoMercy.Plugins.Abstractions;
using Xunit;

namespace NoMercy.Tests.Api.NmComponents;

public class PluginRouteTableTests
{
    private static PluginRouteTable Table()
    {
        return new(
            new PluginRoute { Name = "index", Path = "/", Layout = PluginLayout.Grid },
            new PluginRoute { Name = "new", Path = "/stations/new", Layout = PluginLayout.Form },
            new PluginRoute
            {
                Name = "station",
                Path = "/stations/:id",
                Layout = PluginLayout.ListDetail,
                LayoutBySurface = { [PluginSurface.Tv] = PluginLayout.Immersive }
            },
            new PluginRoute
            {
                Name = "settings",
                Path = "/settings",
                Layout = PluginLayout.Form,
                Surfaces = [PluginSurface.Web]
            });
    }

    [Fact]
    public void PullsParametersOutOfThePath()
    {
        PluginRouteMatch? match = Table().Resolve("/stations/42");

        Assert.Equal("station", match!.Route.Name);
        Assert.Equal("42", match.Param("id"));
    }

    [Fact]
    public void PrefersALiteralSegmentOverAParameter()
    {
        // Otherwise /stations/new reaches the station page looking for a station
        // called "new", and the page the author wrote is unreachable.
        Assert.Equal("new", Table().Resolve("/stations/new")!.Route.Name);
    }

    [Fact]
    public void DecodesAParameterThatWasEscapedToSurviveTheUrl()
    {
        Assert.Equal("jazz & blues", Table().Resolve("/stations/jazz%20%26%20blues")!.Param("id"));
    }

    [Fact]
    public void FindsNothingRatherThanGuessing()
    {
        // Null and "matched with no parameters" must not be the same answer, or
        // an unknown path renders whichever page sorted first.
        Assert.Null(Table().Resolve("/nowhere"));
        Assert.NotNull(Table().Resolve("/"));
        Assert.Empty(Table().Resolve("/")!.Parameters);
    }

    [Fact]
    public void BuildsAPathSoNobodyWritesItTwice()
    {
        // Linking by name is what lets a path be rewritten without hunting for
        // everything that pointed at it.
        Assert.Equal("/stations/42", Table().PathTo("station", new Dictionary<string, string> { ["id"] = "42" }));
        Assert.Equal("/", Table().PathTo("index"));
    }

    [Fact]
    public void EscapesWhatWouldOtherwiseBreakThePath()
    {
        string path = Table().PathTo("station", new Dictionary<string, string> { ["id"] = "jazz & blues" });

        Assert.Equal("/stations/jazz%20%26%20blues", path);
        Assert.Equal("jazz & blues", Table().Resolve(path)!.Param("id"));
    }

    [Fact]
    public void RefusesToBuildALinkItCannotFillIn()
    {
        Assert.Throws<ArgumentException>(() => Table().PathTo("station"));
        Assert.Throws<ArgumentException>(() => Table().PathTo("nothing-called-this"));
    }

    [Fact]
    public void RefusesTwoRoutesClaimingOnePath()
    {
        // Behaviour would depend on declaration order, and whichever lost is a
        // page nobody can reach.
        Assert.Throws<ArgumentException>(() => new PluginRouteTable(
            new PluginRoute { Name = "a", Path = "/stations/:id" },
            new PluginRoute { Name = "b", Path = "/stations/:slug" }));
    }

    [Fact]
    public void RefusesTwoRoutesSharingOneName()
    {
        Assert.Throws<ArgumentException>(() => new PluginRouteTable(
            new PluginRoute { Name = "same", Path = "/one" },
            new PluginRoute { Name = "same", Path = "/two" }));
    }

    [Fact]
    public void ListsOnlyThePagesASurfaceCanOpen()
    {
        Assert.Contains(Table().On(PluginSurface.Web), route => route.Name == "settings");
        Assert.DoesNotContain(Table().On(PluginSurface.Tv), route => route.Name == "settings");
    }

    [Fact]
    public void GivesAPageADifferentShellWhereOneShapeCannotServe()
    {
        PluginRoute station = Table().Routes.First(route => route.Name == "station");

        Assert.Equal(PluginLayout.Immersive, station.LayoutFor(PluginSurface.Tv));
        Assert.Equal(PluginLayout.ListDetail, station.LayoutFor(PluginSurface.Web));
    }

    [Fact]
    public void LinksBetweenPagesWithoutWritingThePrefix()
    {
        PluginActionIntent intent = Table().GoTo("station", new Dictionary<string, string> { ["id"] = "42" });
        Guid id = Guid.NewGuid();

        Assert.Equal($"/music/plugins/{id}/stations/42", PluginNavigation.Resolve(intent, PluginKind.Music, id));
    }
}
