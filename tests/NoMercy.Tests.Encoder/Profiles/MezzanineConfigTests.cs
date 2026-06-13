using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles;

public class MezzanineConfigTests
{
    [Fact]
    public void Default_IsVisuallyLosslessHevc()
    {
        MezzanineConfig mezzanine = new();

        mezzanine.Codec.Should().Be("hevc");
        mezzanine.Crf.Should().Be(12, "visually lossless by default");
    }

    [Fact]
    public void EncodingProfile_Mezzanine_DefaultsNull_AndIsSettable()
    {
        EncodingProfile noMezzanine = new(
            Id: Ulid.NewUlid(),
            Name: "p",
            Container: Container.HlsTs,
            Video: null,
            Audio: [],
            Subtitles: []
        );

        noMezzanine.Mezzanine.Should().BeNull("default = no mezzanine, unchanged behaviour");

        EncodingProfile withMezzanine = noMezzanine with
        {
            Mezzanine = new MezzanineConfig(Codec: "ffv1", Crf: 0),
        };

        withMezzanine.Mezzanine.Should().NotBeNull();
        withMezzanine.Mezzanine!.Codec.Should().Be("ffv1");
    }
}
