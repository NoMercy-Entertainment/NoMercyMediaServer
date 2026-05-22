using NoMercy.Encoder.Format.Container;

namespace NoMercy.Tests.Encoder;

[Trait("Category", "Unit")]
public class ContainerFactoryTests
{
    // The seeded EncoderProfile rows use these literal Container values via the
    // VideoContainers / AudioContainers constants — every one must round-trip
    // through BaseContainer.Create to land on the right concrete container type.
    [Theory]
    [InlineData("m3u8", typeof(Hls))]
    [InlineData("hls", typeof(Hls))]
    [InlineData("mp4", typeof(Mp4))]
    [InlineData("MP4", typeof(Mp4))] // case-insensitive: profile data has used both
    [InlineData("mkv", typeof(Mkv))]
    [InlineData("mp3", typeof(Mp3))]
    [InlineData("flac", typeof(Flac))]
    [InlineData("m4a", typeof(M4a))]
    [InlineData("ogg", typeof(Ogg))]
    [InlineData("webm", typeof(WebM))]
    public void Create_ReturnsExpectedContainerForKnownProfileValue(
        string profileContainer,
        Type expectedType
    )
    {
        BaseContainer container = BaseContainer.Create(profileContainer);
        Assert.IsType(expectedType, container);
    }

    [Fact]
    public void Create_UnknownContainer_Throws()
    {
        Assert.Throws<Exception>(() => BaseContainer.Create("realmedia"));
    }

    [Theory]
    [InlineData(typeof(Hls), true)]
    [InlineData(typeof(Mp4), true)]
    [InlineData(typeof(Mkv), true)]
    [InlineData(typeof(WebM), true)]
    [InlineData(typeof(Mp3), false)]
    [InlineData(typeof(Flac), false)]
    [InlineData(typeof(M4a), false)]
    [InlineData(typeof(Ogg), false)]
    public void SupportsSpriteStream_AudioOnlyContainersOptOut(Type containerType, bool expected)
    {
        BaseContainer container = (BaseContainer)Activator.CreateInstance(containerType)!;
        Assert.Equal(expected, container.SupportsSpriteStream);
    }

    [Theory]
    [InlineData(typeof(Hls), true)]
    [InlineData(typeof(Mp4), false)]
    [InlineData(typeof(Mkv), false)]
    [InlineData(typeof(WebM), false)]
    [InlineData(typeof(Mp3), false)]
    [InlineData(typeof(Flac), false)]
    [InlineData(typeof(M4a), false)]
    [InlineData(typeof(Ogg), false)]
    public void SupportsMasterPlaylist_OnlyAdaptiveStreaming(Type containerType, bool expected)
    {
        BaseContainer container = (BaseContainer)Activator.CreateInstance(containerType)!;
        Assert.Equal(expected, container.SupportsMasterPlaylist);
    }

    [Theory]
    [InlineData(typeof(Hls), true)]
    [InlineData(typeof(Mp4), true)]
    [InlineData(typeof(Mkv), true)]
    [InlineData(typeof(WebM), true)]
    [InlineData(typeof(Mp3), false)]
    [InlineData(typeof(Flac), false)]
    [InlineData(typeof(M4a), false)]
    [InlineData(typeof(Ogg), false)]
    public void SupportsFontsExtraction_AudioOnlyContainersOptOut(Type containerType, bool expected)
    {
        BaseContainer container = (BaseContainer)Activator.CreateInstance(containerType)!;
        Assert.Equal(expected, container.SupportsFontsExtraction);
    }
}
