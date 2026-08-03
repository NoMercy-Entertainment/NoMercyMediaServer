using Newtonsoft.Json;
using NoMercy.Api.DTOs.Media.Nm;
using Xunit;

namespace NoMercy.Tests.Api.NmComponents;

public class NmBuilderTests
{
    private static string Json(NmComponent component)
    {
        return JsonConvert.SerializeObject(component);
    }

    [Fact]
    public void ReadsAsATreeAndSerialisesAsOne()
    {
        // The shape a developer is meant to write: outside in, one call per
        // decision, nesting that looks like nesting.
        NmComponent card = Nm.Card("movie")
            .Pad("4")
            .Gap("2")
            .Color("plum")
            .Add(
                Nm.Badge("quality").Text("4K"),
                Nm.Button("play").Text("Play")
            );

        string json = Json(card);

        Assert.Equal("NMCard", card.Component);
        Assert.Contains("\"color\":\"plum\"", json);
        Assert.Contains("\"all\":\"4\"", json);
        Assert.Contains("NMBadge", json);
        Assert.Contains("NMButton", json);
        Assert.Contains("\"text\":\"Play\"", json);
    }

    [Fact]
    public void KnowsItsOwnNameWithoutBeingTold()
    {
        // The implicit conversion is what lets a built-up component sit directly
        // in a parent's Add(), so the name has to come from the props themselves.
        NmComponent badge = Nm.Badge("q").Text("4K");

        Assert.Equal("NMBadge", badge.Component);
        Assert.Equal("q", badge.Id);
    }

    [Fact]
    public void NamesTheComponentTheClientDispatchesOn()
    {
        NmComponent button = NmBuilder.Button(new() { Id = "play" });

        Assert.Equal("NMButton", button.Component);
        Assert.Equal("play", button.Id);
    }

    [Fact]
    public void CarriesTheTypedPropsOntoTheWire()
    {
        NmComponent badge = NmBuilder.Badge(new()
        {
            Id = "quality",
            Color = "plum",
            Text = "4K"
        });

        string json = Json(badge);

        Assert.Contains("\"color\":\"plum\"", json);
        Assert.Contains("\"text\":\"4K\"", json);
    }

    [Fact]
    public void OmitsWhatThePayloadDoesNotName()
    {
        // A null written onto the wire is not the same as saying nothing: the
        // client would take it as an instruction and lose the component's own
        // default.
        NmComponent badge = NmBuilder.Badge(new() { Id = "b" });

        string json = Json(badge);

        Assert.DoesNotContain("\"color\":null", json);
        Assert.DoesNotContain("\"color\"", json);
    }

    [Fact]
    public void NestsChildrenSoAComponentCanHoldComponents()
    {
        NmComponent card = NmBuilder.Card(
            new() { Id = "movie" },
            NmBuilder.Badge(new() { Id = "quality", Text = "4K" }),
            NmBuilder.Button(new() { Id = "play" })
        );

        string json = Json(card);

        Assert.Contains("NMBadge", json);
        Assert.Contains("NMButton", json);
    }

    [Fact]
    public void AppendsChildrenRatherThanReplacingThem()
    {
        // Props carrying items and a call passing more must end with both, or
        // whichever the caller wrote second silently wins.
        NmComponent first = NmBuilder.Badge(new() { Id = "a" });
        NmComponent second = NmBuilder.Badge(new() { Id = "b" });

        NmComponent card = NmBuilder.Card(new() { Id = "c", Items = [first] }, second);
        string json = Json(card);

        Assert.Contains("\"id\":\"a\"", json);
        Assert.Contains("\"id\":\"b\"", json);
    }

    [Fact]
    public void CarriesTheBoxSoTheServerOwnsTheLayout()
    {
        NmComponent card = NmBuilder.Card(new()
        {
            Id = "c",
            Box = new() { Padding = new() { All = "4" } }
        });

        Assert.Contains("\"padding\"", Json(card));
        Assert.Contains("\"all\":\"4\"", Json(card));
    }
}
