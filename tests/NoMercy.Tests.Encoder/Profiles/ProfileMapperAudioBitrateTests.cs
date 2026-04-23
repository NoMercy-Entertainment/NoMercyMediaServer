namespace NoMercy.Tests.Encoder.Profiles;

using NoMercy.Encoder.Profiles;

/// <summary>
/// V1 audio profiles don't store a bitrate — ProfileMapper has to pick one.
/// Hardcoding 192 kbps (the old behavior) produces audibly wrong output
/// on Dolby / DTS / lossless codecs. These tests verify the mapper uses
/// each codec's own default instead.
/// </summary>
public class ProfileMapperAudioBitrateTests
{
    [Theory]
    [InlineData("aac", 192)] // libfdk_aac default
    [InlineData("opus", 128)] // libopus default
    [InlineData("ac3", 384)] // AC3 5.1 sweet spot
    [InlineData("eac3", 640)] // E-AC3 spec default
    [InlineData("mp3", 192)] // libmp3lame default
    [InlineData("vorbis", 192)] // libvorbis default
    [InlineData("dts", 768)] // DCA spec default
    public void LossyCodec_UsesCodecDefaultBitrate(string codecString, int expectedKbps)
    {
        V1AudioProfile v1 = MakeAudio(codecString, channels: 2);

        EncodingProfile profile = ProfileMapper.FromV1(
            id: Ulid.NewUlid(),
            name: "t",
            container: "hls",
            videoProfiles: [],
            audioProfiles: [v1],
            subtitleProfiles: []
        );

        AudioOutput audio = Assert.Single(profile.AudioOutputs);
        Assert.Equal(expectedKbps, audio.BitrateKbps);
    }

    [Theory]
    [InlineData("flac")]
    [InlineData("truehd")]
    public void LosslessCodec_BitrateIsZero(string codecString)
    {
        // Lossless encoders ignore bitrate; setting anything non-zero makes
        // the profile validator complain about "bitrate below min (0)".
        V1AudioProfile v1 = MakeAudio(codecString, channels: 2);

        EncodingProfile profile = ProfileMapper.FromV1(
            id: Ulid.NewUlid(),
            name: "t",
            container: "mkv",
            videoProfiles: [],
            audioProfiles: [v1],
            subtitleProfiles: []
        );

        AudioOutput audio = Assert.Single(profile.AudioOutputs);
        Assert.Equal(0, audio.BitrateKbps);
    }

    private static V1AudioProfile MakeAudio(string codec, int channels) =>
        new(
            Codec: codec,
            Channels: channels,
            SampleRate: 48000,
            SegmentName: "",
            PlaylistName: "",
            AllowedLanguages: [],
            CustomArguments: []
        );

    [Fact]
    public void MappedAudio_PassesProfileValidator_PerCodec()
    {
        // Pre-fix: AC3 5.1 at hardcoded 192 kbps would fail validation
        // (AC3 range 32-640, so 192 is legal but below Dolby spec for 5.1 —
        // not a validator error, but wrong quality). With the fix the
        // default is 384 which is inside every spec. This test proves the
        // mapped profile round-trips cleanly through the validator for
        // every real codec.
        ProfileValidator validator = new(new());
        string[] codecs = ["aac", "opus", "ac3", "eac3", "mp3", "vorbis", "dts", "flac", "truehd"];

        foreach (string codec in codecs)
        {
            int channels = codec switch
            {
                "mp3" => 2,
                "ac3" or "dts" => 6,
                _ => 2,
            };

            V1AudioProfile v1 = MakeAudio(codec, channels);

            EncodingProfile profile = ProfileMapper.FromV1(
                id: Ulid.NewUlid(),
                name: $"t-{codec}",
                container: "mkv",
                videoProfiles: [],
                audioProfiles: [v1],
                subtitleProfiles: []
            );

            ValidationResult result = validator.Validate(profile);
            Assert.True(
                result.IsValid,
                $"{codec}: {string.Join(", ", result.Errors.Select(e => e.Message))}"
            );
        }
    }
}
