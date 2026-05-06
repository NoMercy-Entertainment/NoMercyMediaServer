using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles;

public class EncodingProfileHlsDerivativesTests
{
    // ── When no HlsDerivatives is set, the field is null ─────────────────────

    [Fact]
    public void HlsDerivatives_DefaultsToNull_WhenNotProvided()
    {
        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "test",
            Container: Container.HlsFmp4,
            Video: null,
            Audio: [],
            Subtitles: []
        );

        profile.HlsDerivatives.Should().BeNull();
    }

    // ── Null coalesces to a default-constructed HlsDerivatives ────────────────

    [Fact]
    public void HlsDerivatives_NullCoalesces_ToDefaults()
    {
        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "test",
            Container: Container.HlsFmp4,
            Video: null,
            Audio: [],
            Subtitles: []
        );

        HlsDerivatives effective = profile.HlsDerivatives ?? new HlsDerivatives();

        effective.GenerateMasterPlaylist.Should().BeTrue();
        effective.GenerateSpriteVtt.Should().BeTrue();
        effective.GenerateChapters.Should().BeTrue();
        effective.GenerateFontsJson.Should().BeTrue();
    }

    // ── Explicit override survives round-trip ─────────────────────────────────

    [Fact]
    public void HlsDerivatives_Override_IsPropagated()
    {
        HlsDerivatives override_ = new() { GenerateIFramePlaylists = true };
        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "test",
            Container: Container.HlsFmp4,
            Video: null,
            Audio: [],
            Subtitles: [],
            HlsDerivatives: override_
        );

        profile.HlsDerivatives.Should().NotBeNull();
        profile.HlsDerivatives!.GenerateIFramePlaylists.Should().BeTrue();
    }
}
