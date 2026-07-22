// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using NoMercy.Encoder.Naming;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Naming;

public class OutputNamingResolverTests
{
    private readonly OutputNamingResolver _resolver = new(mediaKeys: new MediaKeyResolver());

    // ------------------------------------------------------------------
    // C2: Bundle-level path resolution
    // ------------------------------------------------------------------

    [Fact]
    public void Resolve_ArchiveRemuxMkv_ProducesSingleFilePaths()
    {
        EncodingProfile profile = TestProfiles.ArchiveRemuxMkv();
        MediaItemRef media = new(Type: MediaType.Movie, Id: 550, Title: "Fight Club", Year: 1999);

        BundleLayout layout = _resolver.Resolve(media: media, profile: profile);

        layout.IsSingleFile.Should().BeTrue();
        layout.MediaKey.Should().Be(expected: "mfa");
        layout.SingleFileName.Should().Be(expected: "Fight Club.(1999).NoMercy.mkv");
        layout.ManifestPath.Should().Be(expected: "Fight Club.(1999).NoMercy.manifest.json");
        layout.ReconstructionPath.Should().Be(expected: "Fight Club.(1999).NoMercy.reconstruction.json");
        layout.BundleDirectory.Should().Be(expected: string.Empty);
        layout.MasterPlaylistName.Should().Be(expected: string.Empty);
    }

    [Fact]
    public void Resolve_HlsFmp4Preset_ProducesBundleDir()
    {
        EncodingProfile profile = TestProfiles.WebHls1080p();
        MediaItemRef media = new(Type: MediaType.Movie, Id: 550, Title: "Fight Club", Year: 1999);

        BundleLayout layout = _resolver.Resolve(media: media, profile: profile);

        layout.IsSingleFile.Should().BeFalse();
        layout.MediaKey.Should().Be(expected: "mfa");
        layout.PresetSlug.Should().Be(expected: "web-1080p");
        layout.BundleDirectory.Should().Be(expected: "encodes/web-1080p");
        layout.MasterPlaylistName.Should().Be(expected: "mfa_master.m3u8");
        layout.ManifestPath.Should().Be(expected: "encodes/web-1080p/manifest.json");
        layout.ReconstructionPath.Should().Be(expected: "encodes/web-1080p/reconstruction.json");
    }

    [Fact]
    public void Resolve_LongPresetName_TruncatesSlugTo24Chars()
    {
        EncodingProfile profile = TestProfiles.WithName(name: "This Is A Really Long Preset Name");
        MediaItemRef media = new(Type: MediaType.Movie, Id: 1, Title: "X", Year: 2026);

        BundleLayout layout = _resolver.Resolve(media: media, profile: profile);

        layout.PresetSlug.Length.Should().BeLessThanOrEqualTo(expected: 24);
        layout.PresetSlug.Should().Be(expected: "this-is-a-really-long-pr"); // first 24 chars of slug
    }

    [Fact]
    public void Resolve_NameSplitsAtSeparator_StripsTrailingDash()
    {
        // "abcdefghijklmnopqrstuvw Foo" slugifies to 'abcdefghijklmnopqrstuvw-foo'
        // (27 chars). Truncating to 24 lands on 'abcdefghijklmnopqrstuvw-' — the
        // trailing dash must be trimmed off so we never persist 'xxx-' slugs.
        EncodingProfile profile = TestProfiles.WithName(name: "abcdefghijklmnopqrstuvw Foo");
        MediaItemRef media = new(Type: MediaType.Movie, Id: 1, Title: "X", Year: 2026);

        BundleLayout layout = _resolver.Resolve(media: media, profile: profile);

        layout.PresetSlug.Should().Be(expected: "abcdefghijklmnopqrstuvw");
        layout.PresetSlug.Should().NotEndWith(unexpected: "-");
    }

    // ------------------------------------------------------------------
    // C3: Per-output path resolution
    // ------------------------------------------------------------------

    private BundleLayout WebHls1080pLayoutForFightClub()
    {
        EncodingProfile profile = TestProfiles.WebHls1080p();
        MediaItemRef media = new(Type: MediaType.Movie, Id: 550, Title: "Fight Club", Year: 1999);
        return _resolver.Resolve(media: media, profile: profile);
    }

    [Fact]
    public void VideoVariantPath_HlsFmp4_ProducesNestedPath()
    {
        BundleLayout layout = WebHls1080pLayoutForFightClub();
        string path = _resolver.VideoVariantPath(layout: layout, label: "1080p", filename: "init.mp4");
        path.Should().Be(expected: "encodes/web-1080p/video/1080p/mfa_1080p_init.mp4");
    }

    [Fact]
    public void VideoVariantSegmentPath_AppliesSeqFormatter()
    {
        BundleLayout layout = WebHls1080pLayoutForFightClub();
        string path = _resolver.VideoSegmentPath(layout: layout, label: "1080p", seq: 1);
        path.Should().Be(expected: "encodes/web-1080p/video/1080p/mfa_1080p_00001.m4s");
    }

    [Fact]
    public void AudioRenditionPath_UsesLangAndCodecFolder()
    {
        BundleLayout layout = WebHls1080pLayoutForFightClub();
        string path = _resolver.AudioPlaylistPath(layout: layout, language: "eng", codec: "aac");
        path.Should().Be(expected: "encodes/web-1080p/audio/eng-aac/mfa_eng_aac.m3u8");
    }

    [Fact]
    public void SubtitlePath_FlatSubsFolder()
    {
        BundleLayout layout = WebHls1080pLayoutForFightClub();
        string path = _resolver.SubtitlePath(layout: layout, language: "eng", extension: "vtt");
        path.Should().Be(expected: "encodes/web-1080p/subs/mfa_eng.vtt");
    }

    [Fact]
    public void DerivativePath_LivesAtBundleRoot()
    {
        BundleLayout layout = WebHls1080pLayoutForFightClub();
        _resolver
            .DerivativePath(layout: layout, filename: "thumbnails.vtt")
            .Should()
            .Be(expected: "encodes/web-1080p/thumbnails.vtt");
    }

    // ── audio init + segment paths ──────────────────────────────────────────

    [Fact]
    public void AudioInitPath_UsesLangAndCodecFolder()
    {
        BundleLayout layout = WebHls1080pLayoutForFightClub();
        string path = _resolver.AudioInitPath(layout: layout, language: "eng", codec: "aac");
        path.Should().Be(expected: "encodes/web-1080p/audio/eng-aac/mfa_eng_aac_init.mp4");
    }

    [Fact]
    public void AudioSegmentPath_AppliesSeqFormatter()
    {
        BundleLayout layout = WebHls1080pLayoutForFightClub();
        string path = _resolver.AudioSegmentPath(layout: layout, language: "fra", codec: "opus", seq: 42);
        path.Should().Be(expected: "encodes/web-1080p/audio/fra-opus/mfa_fra_opus_00042.m4s");
    }

    // ── single-file container variants ──────────────────────────────────────

    [Theory]
    [InlineData(data: [Container.Mp3, "mp3"])]
    [InlineData(data: [Container.Flac, "flac"])]
    [InlineData(data: [Container.Aac, "aac"])]
    [InlineData(data: [Container.Ogg, "ogg"])]
    [InlineData(data: [Container.Mka, "mka"])]
    [InlineData(data: [Container.Mp4, "mp4"])]
    [InlineData(data: [Container.Mkv, "mkv"])]
    public void Resolve_SingleFileContainer_UsesContainerExtension(
        Container container,
        string expectedExt
    )
    {
        EncodingProfile profile = TestProfiles.WithContainer(container: container);
        MediaItemRef media = new(Type: MediaType.Movie, Id: 550, Title: "Fight Club", Year: 1999);

        BundleLayout layout = _resolver.Resolve(media: media, profile: profile);

        layout.IsSingleFile.Should().BeTrue();
        layout.SingleFileName.Should().Be(expected: $"Fight Club.(1999).NoMercy.{expectedExt}");
    }

    [Fact]
    public void Resolve_NoYear_SkipsYearSuffix()
    {
        // Year=null → no ".(YYYY)" segment between title and NoMercy marker.
        EncodingProfile profile = TestProfiles.WithContainer(container: Container.Mkv);
        MediaItemRef media = new(Type: MediaType.Movie, Id: 7, Title: "Untitled", Year: null);

        BundleLayout layout = _resolver.Resolve(media: media, profile: profile);

        layout.SingleFileName.Should().Be(expected: "Untitled.NoMercy.mkv");
        layout.ManifestPath.Should().Be(expected: "Untitled.NoMercy.manifest.json");
    }

    [Fact]
    public void Resolve_StreamingContainer_StringMapsToFriendlyForm()
    {
        // Pinned: the container string in BundleLayout.ContainerString is the
        // dashed friendly form, never the enum's ToString().
        BundleLayout layout = _resolver.Resolve(
            media: new(Type: MediaType.Movie, Id: 1, Title: "X", Year: null),
            profile: TestProfiles.WebHls1080p()
        );

        layout.ContainerString.Should().Be(expected: "hls-fmp4");
    }

    [Theory]
    [InlineData(data: [Container.HlsTs, "hls-ts"])]
    [InlineData(data: [Container.HlsFmp4, "hls-fmp4"])]
    [InlineData(data: [Container.Dash, "dash"])]
    [InlineData(data: [Container.AudioHlsTs, "audio-hls-ts"])]
    [InlineData(data: [Container.AudioHlsFmp4, "audio-hls-fmp4"])]
    public void Resolve_ContainerString_DashedFriendlyForm(Container container, string expected)
    {
        EncodingProfile profile = TestProfiles.WithContainer(container: container);
        BundleLayout layout = _resolver.Resolve(
            media: new(Type: MediaType.Movie, Id: 1, Title: "X", Year: null),
            profile: profile
        );

        layout.ContainerString.Should().Be(expected: expected);
    }
}
