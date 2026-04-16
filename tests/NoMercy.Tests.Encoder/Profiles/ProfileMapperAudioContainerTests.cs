namespace NoMercy.Tests.Encoder.Profiles;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

public class ProfileMapperAudioContainerTests
{
    [Theory]
    [InlineData("m3u8", OutputFormat.Hls)]
    [InlineData("hls", OutputFormat.Hls)]
    [InlineData("mkv", OutputFormat.Mkv)]
    [InlineData("matroska", OutputFormat.Mkv)]
    [InlineData("mp4", OutputFormat.Mp4)]
    [InlineData("m4a", OutputFormat.Mp4)]
    [InlineData("aac", OutputFormat.Mp4)]
    [InlineData("M4A", OutputFormat.Mp4)]
    [InlineData("AAC", OutputFormat.Mp4)]
    [InlineData("dash", OutputFormat.Dash)]
    [InlineData("mpd", OutputFormat.Dash)]
    [InlineData("mp3", OutputFormat.Mp3)]
    [InlineData("MP3", OutputFormat.Mp3)]
    [InlineData("flac", OutputFormat.Flac)]
    [InlineData("FLAC", OutputFormat.Flac)]
    [InlineData("ogg", OutputFormat.Ogg)]
    [InlineData("oga", OutputFormat.Ogg)]
    [InlineData("opus", OutputFormat.Ogg)]
    [InlineData("unknown", OutputFormat.Hls)]
    public void FromV1_Container_MapsToExpectedFormat(string container, OutputFormat expected)
    {
        EncodingProfile profile = ProfileMapper.FromV1(
            id: Ulid.NewUlid(),
            name: "Test",
            container: container,
            videoProfiles: [],
            audioProfiles: [],
            subtitleProfiles: []
        );

        Assert.Equal(expected, profile.Format);
    }
}
