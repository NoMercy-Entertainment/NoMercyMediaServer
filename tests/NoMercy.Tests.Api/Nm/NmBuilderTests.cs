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
                Nm.Button("play").AriaLabel("Play")
            );

        string json = Json(card);

        Assert.Equal("NMCard", card.Component);
        Assert.Contains("\"color\":\"plum\"", json);
        Assert.Contains("\"all\":\"4\"", json);
        Assert.Contains("NMBadge", json);
        Assert.Contains("NMButton", json);
        Assert.Contains("\"text\":\"4K\"", json);
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
    public void OmitsWhatThePayloadDoesNotName()
    {
        // A null written onto the wire is not the same as saying nothing: the
        // client would take it as an instruction and lose the component's own
        // default.
        NmComponent badge = Nm.Badge("b").Text("x");

        Assert.DoesNotContain("\"color\"", Json(badge));
        Assert.DoesNotContain("null", Json(badge));
    }

    [Fact]
    public void AppendsChildrenRatherThanReplacingThem()
    {
        // Two Add calls must both survive, or whichever was written second
        // silently wins.
        NmComponent card = Nm.Card("c")
            .Add(Nm.Badge("a").Text("1"))
            .Add(Nm.Badge("b").Text("2"));

        string json = Json(card);

        Assert.Contains("\"id\":\"a\"", json);
        Assert.Contains("\"id\":\"b\"", json);
    }

    [Fact]
    public void NestsToAnyDepth()
    {
        NmComponent tree = Nm.Card("outer")
            .Add(Nm.Card("middle")
                .Add(Nm.Badge("inner").Text("deep")));

        Assert.Contains("\"id\":\"inner\"", Json(tree));
        Assert.Contains("\"text\":\"deep\"", Json(tree));
    }

    [Fact]
    public void CarriesTheBoxSoTheServerOwnsTheLayout()
    {
        NmComponent card = Nm.Card("c").Pad("4").Margin("2");

        string json = Json(card);

        Assert.Contains("\"padding\"", json);
        Assert.Contains("\"margin\"", json);
    }

    [Fact]
    public void TakesEveryPaletteFamilyRatherThanAListedFew()
    {
        // The families a component enumerates are the designer's recommendation.
        // The wire accepts any the palette ships.
        Assert.Contains("\"color\":\"bronze\"", Json(Nm.Badge("b").Text("x").Color("bronze")));
        Assert.Contains("\"color\":\"mint\"", Json(Nm.Badge("b").Text("x").Color("mint")));
    }
}
