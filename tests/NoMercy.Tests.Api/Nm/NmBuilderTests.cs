using Newtonsoft.Json;
using NoMercy.Api.DTOs.Media.Nm;
using Xunit;

namespace NoMercy.Tests.Api.Nm;

public class NmBuilderTests
{
    private static string Json(NmComponent component)
    {
        return JsonConvert.SerializeObject(component);
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
