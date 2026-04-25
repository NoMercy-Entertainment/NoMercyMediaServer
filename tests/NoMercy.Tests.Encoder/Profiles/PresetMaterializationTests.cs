namespace NoMercy.Tests.Encoder.Profiles;

using Newtonsoft.Json;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

/// <summary>
/// Tests for the V3→V1 mapping logic that materializes EncodingPreset rows
/// into EncoderProfile-compatible fields for the Folder picker.
/// These are pure-logic tests — no database required.
/// </summary>
public class PresetMaterializationTests
{
    // ──────────────────────────────────────────────────────────────────────
    // 1. Materialize_BuiltinPreset_CreatesEncoderProfile
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Materialize_BuiltinPreset_ProducesEncoderProfileFields()
    {
        EncodingProfile preset = BuiltinPresets.GeneralH264_1080p_Fast();

        (
            Ulid id,
            string name,
            string container,
            string videoJson,
            string audioJson,
            string subtitleJson
        ) = ProfileMapper.ToV1Fields(preset);

        id.Should().Be(preset.Id, "mapped Id must match preset Id");
        name.Should().Be(preset.Name, "mapped Name must match preset Name");
        container.Should().Be("m3u8", "GeneralH264_1080p_Fast uses HLS format");
        videoJson.Should().NotBeNullOrWhiteSpace("video JSON must be populated");
        audioJson.Should().NotBeNullOrWhiteSpace("audio JSON must be populated");
    }

    // ──────────────────────────────────────────────────────────────────────
    // 2. Materialize_ReSeed_DoesNotDuplicate (Id stability)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Materialize_ReSeed_ProducesSameId()
    {
        EncodingProfile preset = BuiltinPresets.GeneralH264_1080p_Fast();

        (Ulid id1, _, _, _, _, _) = ProfileMapper.ToV1Fields(preset);
        (Ulid id2, _, _, _, _, _) = ProfileMapper.ToV1Fields(preset);

        id1.Should()
            .Be(id2, "mapping the same preset twice must yield the same Id — upsert key is stable");
    }

    // ──────────────────────────────────────────────────────────────────────
    // 3. Materialize_ContainerFormat_IsDerivedFromOutputFormat
    // ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(OutputFormat.Hls, "m3u8")]
    [InlineData(OutputFormat.AudioHls, "m3u8")]
    [InlineData(OutputFormat.Mkv, "mkv")]
    [InlineData(OutputFormat.Mp4, "mp4")]
    [InlineData(OutputFormat.Dash, "mpd")]
    [InlineData(OutputFormat.Mp3, "mp3")]
    [InlineData(OutputFormat.Flac, "flac")]
    [InlineData(OutputFormat.Ogg, "ogg")]
    public void Materialize_ContainerFormat_IsDerivedFromOutputFormat(
        OutputFormat format,
        string expectedContainer
    )
    {
        string container = ProfileMapper.ContainerFromFormat(format);
        container
            .Should()
            .Be(
                expectedContainer,
                $"OutputFormat.{format} must map to container '{expectedContainer}'"
            );
    }

    // ──────────────────────────────────────────────────────────────────────
    // Bonus: all 12 built-in presets produce non-empty JSON fields
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Materialize_AllBuiltinPresets_ProduceNonEmptyFields()
    {
        IReadOnlyList<EncodingProfile> all = BuiltinPresets.All();

        foreach (EncodingProfile preset in all)
        {
            (
                Ulid id,
                string name,
                string container,
                string videoJson,
                string audioJson,
                string subtitleJson
            ) = ProfileMapper.ToV1Fields(preset);

            id.Should().NotBe(default(Ulid), $"{preset.Name}: Id must not be default");
            name.Should().NotBeNullOrWhiteSpace($"{preset.Name}: Name must be set");
            container.Should().NotBeNullOrWhiteSpace($"{preset.Name}: Container must be set");

            // Video and audio JSON must be valid JSON arrays (even if empty for music-only presets)
            object? videoArr = JsonConvert.DeserializeObject(videoJson);
            videoArr.Should().NotBeNull($"{preset.Name}: VideoProfilesJson must be valid JSON");

            object? audioArr = JsonConvert.DeserializeObject(audioJson);
            audioArr.Should().NotBeNull($"{preset.Name}: AudioProfilesJson must be valid JSON");
        }
    }
}
