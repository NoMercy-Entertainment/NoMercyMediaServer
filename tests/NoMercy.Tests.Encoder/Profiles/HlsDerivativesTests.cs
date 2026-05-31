using Newtonsoft.Json;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles;

public class HlsDerivativesTests
{
    // ── 1. Default field values ───────────────────────────────────────────────

    [Fact]
    public void Defaults_MatchSpec()
    {
        HlsDerivatives d = new();

        d.GenerateMetadataJson.Should().BeTrue();
        d.GenerateSpriteVtt.Should().BeTrue();
        d.SpriteVttIntervalSeconds.Should().Be(10);
        d.SpriteVttColumns.Should().Be(5);
        d.SpriteVttRows.Should().Be(5);
        d.SpriteVttThumbnailWidth.Should().Be(160);
        d.GenerateChapters.Should().BeTrue();
        d.GenerateFontsJson.Should().BeTrue();
        d.GenerateIFramePlaylists.Should().BeFalse();
        d.GenerateThumbnailTrack.Should().BeTrue();
        d.ExtractClosedCaptions.Should().BeFalse();
        d.GenerateMasterPlaylist.Should().BeTrue();
        d.WriteOriginalFilename.Should().BeTrue();
    }

    // ── 2. Record equality ────────────────────────────────────────────────────

    [Fact]
    public void TwoDefaults_AreEqual()
    {
        HlsDerivatives a = new();
        HlsDerivatives b = new();

        a.Should().Be(b);
    }

    // ── 3. Newtonsoft.Json round-trip ─────────────────────────────────────────

    [Fact]
    public void JsonRoundTrip_PreservesAllFields()
    {
        HlsDerivatives original = new()
        {
            GenerateMetadataJson = false,
            GenerateSpriteVtt = false,
            SpriteVttIntervalSeconds = 30,
            SpriteVttColumns = 8,
            SpriteVttRows = 8,
            SpriteVttThumbnailWidth = 240,
            GenerateChapters = false,
            GenerateFontsJson = false,
            GenerateIFramePlaylists = true,
            GenerateThumbnailTrack = false,
            ExtractClosedCaptions = true,
            GenerateMasterPlaylist = false,
            WriteOriginalFilename = false,
        };

        string json = JsonConvert.SerializeObject(original);
        HlsDerivatives? deserialized = JsonConvert.DeserializeObject<HlsDerivatives>(json);

        deserialized.Should().NotBeNull();
        deserialized.Should().Be(original);
    }

    // ── 4. `with` doesn't mutate other fields ─────────────────────────────────

    [Fact]
    public void With_OneField_LeavesOthersAtDefaults()
    {
        HlsDerivatives defaults = new();
        HlsDerivatives modified = defaults with { GenerateIFramePlaylists = true };

        modified.GenerateIFramePlaylists.Should().BeTrue();
        modified.GenerateMetadataJson.Should().Be(defaults.GenerateMetadataJson);
        modified.GenerateSpriteVtt.Should().Be(defaults.GenerateSpriteVtt);
        modified.SpriteVttIntervalSeconds.Should().Be(defaults.SpriteVttIntervalSeconds);
        modified.SpriteVttColumns.Should().Be(defaults.SpriteVttColumns);
        modified.SpriteVttRows.Should().Be(defaults.SpriteVttRows);
        modified.SpriteVttThumbnailWidth.Should().Be(defaults.SpriteVttThumbnailWidth);
        modified.GenerateChapters.Should().Be(defaults.GenerateChapters);
        modified.GenerateFontsJson.Should().Be(defaults.GenerateFontsJson);
        modified.GenerateThumbnailTrack.Should().Be(defaults.GenerateThumbnailTrack);
        modified.ExtractClosedCaptions.Should().Be(defaults.ExtractClosedCaptions);
        modified.GenerateMasterPlaylist.Should().Be(defaults.GenerateMasterPlaylist);
        modified.WriteOriginalFilename.Should().Be(defaults.WriteOriginalFilename);
    }
}
