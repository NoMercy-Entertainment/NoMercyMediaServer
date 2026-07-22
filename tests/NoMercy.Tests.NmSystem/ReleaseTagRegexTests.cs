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

namespace NoMercy.Tests.NmSystem;

/// <summary>
/// Corpus for the per-category scene-tag regexes that back <see
/// cref="StringExtensions.CleanReleaseTitle"/>. Each named table is exercised
/// directly (so a vocabulary change can never silently break one category) and
/// the category reported by <see cref="StringExtensions.TryGetReleaseTag"/> is
/// pinned for representative release names.
/// </summary>
public class ReleaseTagRegexTests
{
    [Theory]
    [InlineData(data: "480p")]
    [InlineData(data: "720p")]
    [InlineData(data: "1080p")]
    [InlineData(data: "1080i")]
    [InlineData(data: "2160p")]
    [InlineData(data: "4k")]
    [InlineData(data: "8K")]
    [InlineData(data: "UHD")]
    public void Resolution_Matches(string token) =>
        StringExtensions.MatchResolutionTag().IsMatch(input: token).Should().BeTrue();

    [Theory]
    [InlineData(data: "WEB-DL")]
    [InlineData(data: "WEBRip")]
    [InlineData(data: "BluRay")]
    [InlineData(data: "BDRip")]
    [InlineData(data: "HDTV")]
    [InlineData(data: "DVDRip")]
    [InlineData(data: "REMUX")]
    [InlineData(data: "AMZN")]
    [InlineData(data: "DSNP")]
    [InlineData(data: "HMAX")]
    [InlineData(data: "NFLX")]
    [InlineData(data: "HDCAM")]
    public void Source_Matches(string token) =>
        StringExtensions.MatchSourceTag().IsMatch(input: token).Should().BeTrue();

    [Theory]
    [InlineData(data: "x264")]
    [InlineData(data: "x265")]
    [InlineData(data: "H.264")]
    [InlineData(data: "H265")]
    [InlineData(data: "HEVC")]
    [InlineData(data: "XviD")]
    [InlineData(data: "DivX")]
    [InlineData(data: "AVC")]
    [InlineData(data: "VC-1")]
    [InlineData(data: "VP9")]
    public void Codec_Matches(string token) =>
        StringExtensions.MatchCodecTag().IsMatch(input: token).Should().BeTrue();

    [Theory]
    [InlineData(data: "DDP5.1")]
    [InlineData(data: "DD5.1")]
    [InlineData(data: "EAC3")]
    [InlineData(data: "AC3")]
    [InlineData(data: "DTS")]
    [InlineData(data: "DTS-HD")]
    [InlineData(data: "TrueHD")]
    [InlineData(data: "Atmos")]
    [InlineData(data: "AAC")]
    [InlineData(data: "AAC2.0")]
    [InlineData(data: "FLAC")]
    [InlineData(data: "MP3")]
    public void Audio_Matches(string token) =>
        StringExtensions.MatchAudioTag().IsMatch(input: token).Should().BeTrue();

    [Theory]
    [InlineData(data: "10bit")]
    [InlineData(data: "8bit")]
    [InlineData(data: "HDR")]
    [InlineData(data: "HDR10")]
    [InlineData(data: "HDR10+")]
    [InlineData(data: "DoVi")]
    [InlineData(data: "Dolby Vision")]
    [InlineData(data: "HLG")]
    public void Hdr_Matches(string token) =>
        StringExtensions.MatchHdrTag().IsMatch(input: token).Should().BeTrue();

    [Theory]
    [InlineData(data: "REPACK")]
    [InlineData(data: "MULTI")]
    [InlineData(data: "IMAX")]
    public void Flag_Matches(string token) =>
        StringExtensions.MatchFlagTag().IsMatch(input: token).Should().BeTrue();

    // Real title words that merely contain a token substring must never match.
    [Theory]
    [InlineData(data: "Limitless")]
    [InlineData(data: "Therapy")]
    [InlineData(data: "Website")]
    [InlineData(data: "Account")]
    [InlineData(data: "Multitude")]
    public void TitleWords_AreNotTags(string word)
    {
        StringExtensions.MatchResolutionTag().IsMatch(input: word).Should().BeFalse();
        StringExtensions.MatchSourceTag().IsMatch(input: word).Should().BeFalse();
        StringExtensions.MatchCodecTag().IsMatch(input: word).Should().BeFalse();
        StringExtensions.MatchAudioTag().IsMatch(input: word).Should().BeFalse();
        StringExtensions.MatchHdrTag().IsMatch(input: word).Should().BeFalse();
        StringExtensions.MatchFlagTag().IsMatch(input: word).Should().BeFalse();
    }

    [Theory]
    [InlineData(data: ["The Office 1080p WEB-DL", StringExtensions.ReleaseTagCategory.Resolution, "1080p"])]
    [InlineData(data: ["Some Show WEB-DL x265", StringExtensions.ReleaseTagCategory.Source, "WEB-DL"])]
    [InlineData(data: ["Movie Title HEVC", StringExtensions.ReleaseTagCategory.Codec, "HEVC"])]
    [InlineData(data: ["Concert EAC3", StringExtensions.ReleaseTagCategory.Audio, "EAC3"])]
    [InlineData(data: ["Film HDR10", StringExtensions.ReleaseTagCategory.Hdr, "HDR10"])]
    [InlineData(data: ["Feature IMAX", StringExtensions.ReleaseTagCategory.Flag, "IMAX"])]
    public void TryGetReleaseTag_ReportsEarliestCategory(
        string input, StringExtensions.ReleaseTagCategory expectedCategory, string expectedValue)
    {
        bool found = input.TryGetReleaseTag(value: out string value, category: out StringExtensions.ReleaseTagCategory category);

        found.Should().BeTrue();
        category.Should().Be(expected: expectedCategory);
        value.Should().BeEquivalentTo(expected: expectedValue);
    }

    [Fact]
    public void TryGetReleaseTag_ReturnsFalse_ForCleanTitle()
    {
        "The West Wing".TryGetReleaseTag(value: out _, category: out _).Should().BeFalse();
    }
}
