using FluentAssertions;
using NoMercy.Encoder.Naming;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Naming;

public class OutputNamingResolverTests
{
    private readonly OutputNamingResolver _resolver = new(new MediaKeyResolver());

    // ------------------------------------------------------------------
    // C2: Bundle-level path resolution
    // ------------------------------------------------------------------

    [Fact]
    public void Resolve_ArchiveRemuxMkv_ProducesSingleFilePaths()
    {
        EncodingProfile profile = TestProfiles.ArchiveRemuxMkv();
        MediaItemRef media = new(MediaType.Movie, 550, "Fight Club", 1999);

        BundleLayout layout = _resolver.Resolve(media, profile);

        layout.IsSingleFile.Should().BeTrue();
        layout.MediaKey.Should().Be("mfa");
        layout.SingleFileName.Should().Be("Fight Club.(1999).NoMercy.mkv");
        layout.ManifestPath.Should().Be("Fight Club.(1999).NoMercy.manifest.json");
        layout.ReconstructionPath.Should().Be("Fight Club.(1999).NoMercy.reconstruction.json");
        layout.BundleDirectory.Should().Be(string.Empty);
        layout.MasterPlaylistName.Should().Be(string.Empty);
    }

    [Fact]
    public void Resolve_HlsFmp4Preset_ProducesBundleDir()
    {
        EncodingProfile profile = TestProfiles.WebHls1080p();
        MediaItemRef media = new(MediaType.Movie, 550, "Fight Club", 1999);

        BundleLayout layout = _resolver.Resolve(media, profile);

        layout.IsSingleFile.Should().BeFalse();
        layout.MediaKey.Should().Be("mfa");
        layout.PresetSlug.Should().Be("web-1080p");
        layout.BundleDirectory.Should().Be("encodes/web-1080p");
        layout.MasterPlaylistName.Should().Be("mfa_master.m3u8");
        layout.ManifestPath.Should().Be("encodes/web-1080p/manifest.json");
        layout.ReconstructionPath.Should().Be("encodes/web-1080p/reconstruction.json");
    }

    [Fact]
    public void Resolve_LongPresetName_TruncatesSlugTo24Chars()
    {
        EncodingProfile profile = TestProfiles.WithName("This Is A Really Long Preset Name");
        MediaItemRef media = new(MediaType.Movie, 1, "X", 2026);

        BundleLayout layout = _resolver.Resolve(media, profile);

        layout.PresetSlug.Length.Should().BeLessThanOrEqualTo(24);
        layout.PresetSlug.Should().Be("this-is-a-really-long-pr"); // first 24 chars of slug
    }

    [Fact]
    public void Resolve_NameSplitsAtSeparator_StripsTrailingDash()
    {
        // "abcdefghijklmnopqrstuvw Foo" slugifies to 'abcdefghijklmnopqrstuvw-foo'
        // (27 chars). Truncating to 24 lands on 'abcdefghijklmnopqrstuvw-' — the
        // trailing dash must be trimmed off so we never persist 'xxx-' slugs.
        EncodingProfile profile = TestProfiles.WithName("abcdefghijklmnopqrstuvw Foo");
        MediaItemRef media = new(MediaType.Movie, 1, "X", 2026);

        BundleLayout layout = _resolver.Resolve(media, profile);

        layout.PresetSlug.Should().Be("abcdefghijklmnopqrstuvw");
        layout.PresetSlug.Should().NotEndWith("-");
    }

    // ------------------------------------------------------------------
    // C3: Per-output path resolution
    // ------------------------------------------------------------------

    private BundleLayout WebHls1080pLayoutForFightClub()
    {
        EncodingProfile profile = TestProfiles.WebHls1080p();
        MediaItemRef media = new(MediaType.Movie, 550, "Fight Club", 1999);
        return _resolver.Resolve(media, profile);
    }

    [Fact]
    public void VideoVariantPath_HlsFmp4_ProducesNestedPath()
    {
        BundleLayout layout = WebHls1080pLayoutForFightClub();
        string path = _resolver.VideoVariantPath(layout, label: "1080p", filename: "init.mp4");
        path.Should().Be("encodes/web-1080p/video/1080p/mfa_1080p_init.mp4");
    }

    [Fact]
    public void VideoVariantSegmentPath_AppliesSeqFormatter()
    {
        BundleLayout layout = WebHls1080pLayoutForFightClub();
        string path = _resolver.VideoSegmentPath(layout, label: "1080p", seq: 1);
        path.Should().Be("encodes/web-1080p/video/1080p/mfa_1080p_00001.m4s");
    }

    [Fact]
    public void AudioRenditionPath_UsesLangAndCodecFolder()
    {
        BundleLayout layout = WebHls1080pLayoutForFightClub();
        string path = _resolver.AudioPlaylistPath(layout, language: "eng", codec: "aac");
        path.Should().Be("encodes/web-1080p/audio/eng-aac/mfa_eng_aac.m3u8");
    }

    [Fact]
    public void SubtitlePath_FlatSubsFolder()
    {
        BundleLayout layout = WebHls1080pLayoutForFightClub();
        string path = _resolver.SubtitlePath(layout, language: "eng", extension: "vtt");
        path.Should().Be("encodes/web-1080p/subs/mfa_eng.vtt");
    }

    [Fact]
    public void DerivativePath_LivesAtBundleRoot()
    {
        BundleLayout layout = WebHls1080pLayoutForFightClub();
        _resolver
            .DerivativePath(layout, "thumbnails.vtt")
            .Should()
            .Be("encodes/web-1080p/thumbnails.vtt");
    }

    // ── audio init + segment paths ──────────────────────────────────────────

    [Fact]
    public void AudioInitPath_UsesLangAndCodecFolder()
    {
        BundleLayout layout = WebHls1080pLayoutForFightClub();
        string path = _resolver.AudioInitPath(layout, language: "eng", codec: "aac");
        path.Should().Be("encodes/web-1080p/audio/eng-aac/mfa_eng_aac_init.mp4");
    }

    [Fact]
    public void AudioSegmentPath_AppliesSeqFormatter()
    {
        BundleLayout layout = WebHls1080pLayoutForFightClub();
        string path = _resolver.AudioSegmentPath(layout, language: "fra", codec: "opus", seq: 42);
        path.Should().Be("encodes/web-1080p/audio/fra-opus/mfa_fra_opus_00042.m4s");
    }

    // ── single-file container variants ──────────────────────────────────────

    [Theory]
    [InlineData(Container.Mp3, "mp3")]
    [InlineData(Container.Flac, "flac")]
    [InlineData(Container.Aac, "aac")]
    [InlineData(Container.Ogg, "ogg")]
    [InlineData(Container.Mka, "mka")]
    [InlineData(Container.Mp4, "mp4")]
    [InlineData(Container.Mkv, "mkv")]
    public void Resolve_SingleFileContainer_UsesContainerExtension(
        Container container,
        string expectedExt
    )
    {
        EncodingProfile profile = TestProfiles.WithContainer(container);
        MediaItemRef media = new(MediaType.Movie, 550, "Fight Club", 1999);

        BundleLayout layout = _resolver.Resolve(media, profile);

        layout.IsSingleFile.Should().BeTrue();
        layout.SingleFileName.Should().Be($"Fight Club.(1999).NoMercy.{expectedExt}");
    }

    [Fact]
    public void Resolve_NoYear_SkipsYearSuffix()
    {
        // Year=null → no ".(YYYY)" segment between title and NoMercy marker.
        EncodingProfile profile = TestProfiles.WithContainer(Container.Mkv);
        MediaItemRef media = new(MediaType.Movie, 7, "Untitled", null);

        BundleLayout layout = _resolver.Resolve(media, profile);

        layout.SingleFileName.Should().Be("Untitled.NoMercy.mkv");
        layout.ManifestPath.Should().Be("Untitled.NoMercy.manifest.json");
    }

    [Fact]
    public void Resolve_StreamingContainer_StringMapsToFriendlyForm()
    {
        // Pinned: the container string in BundleLayout.ContainerString is the
        // dashed friendly form, never the enum's ToString().
        BundleLayout layout = _resolver.Resolve(
            new MediaItemRef(MediaType.Movie, 1, "X", null),
            TestProfiles.WebHls1080p()
        );

        layout.ContainerString.Should().Be("hls-fmp4");
    }

    [Theory]
    [InlineData(Container.HlsTs, "hls-ts")]
    [InlineData(Container.HlsFmp4, "hls-fmp4")]
    [InlineData(Container.Dash, "dash")]
    [InlineData(Container.AudioHlsTs, "audio-hls-ts")]
    [InlineData(Container.AudioHlsFmp4, "audio-hls-fmp4")]
    public void Resolve_ContainerString_DashedFriendlyForm(Container container, string expected)
    {
        EncodingProfile profile = TestProfiles.WithContainer(container);
        BundleLayout layout = _resolver.Resolve(
            new MediaItemRef(MediaType.Movie, 1, "X", null),
            profile
        );

        layout.ContainerString.Should().Be(expected);
    }
}
