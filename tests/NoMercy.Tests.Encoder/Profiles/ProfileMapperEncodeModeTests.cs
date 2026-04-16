namespace NoMercy.Tests.Encoder.Profiles;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

public class ProfileMapperEncodeModeTests
{
    [Fact]
    public void FromV1_NullEncodeMode_DefaultsToSinglePass()
    {
        EncodingProfile profile = BuildProfile(encodeMode: null);

        Assert.Equal(EncodeMode.SinglePass, profile.EncodeMode);
    }

    [Fact]
    public void FromV1_EmptyEncodeMode_DefaultsToSinglePass()
    {
        EncodingProfile profile = BuildProfile(encodeMode: "");

        Assert.Equal(EncodeMode.SinglePass, profile.EncodeMode);
    }

    [Theory]
    [InlineData("2pass")]
    [InlineData("twopass")]
    [InlineData("two_pass")]
    [InlineData("two-pass")]
    [InlineData("TwoPass")]
    [InlineData("TWOPASS")]
    public void FromV1_TwoPassVariants_MapToTwoPass(string value)
    {
        EncodingProfile profile = BuildProfile(encodeMode: value);

        Assert.Equal(EncodeMode.TwoPass, profile.EncodeMode);
    }

    [Theory]
    [InlineData("singlepass")]
    [InlineData("single")]
    [InlineData("unknown")]
    public void FromV1_NonTwoPassValues_MapToSinglePass(string value)
    {
        EncodingProfile profile = BuildProfile(encodeMode: value);

        Assert.Equal(EncodeMode.SinglePass, profile.EncodeMode);
    }

    private static EncodingProfile BuildProfile(string? encodeMode) =>
        ProfileMapper.FromV1(
            id: Ulid.NewUlid(),
            name: "Test",
            container: "m3u8",
            videoProfiles:
            [
                new V1VideoProfile(
                    Codec: "h264",
                    Bitrate: 4000,
                    Width: 1920,
                    Height: 1080,
                    Preset: "medium",
                    Profile: "high",
                    Tune: "",
                    Level: "4.1",
                    SegmentName: "",
                    PlaylistName: "",
                    ColorSpace: "yuv420p",
                    Crf: 23,
                    KeyInt: 2,
                    ConvertHdrToSdr: false,
                    CustomArguments: []
                ),
            ],
            audioProfiles: [],
            subtitleProfiles: [],
            thumbnailProfile: null,
            encodeMode: encodeMode
        );
}
