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
}
