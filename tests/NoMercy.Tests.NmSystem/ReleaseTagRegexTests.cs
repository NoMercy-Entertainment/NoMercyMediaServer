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
    [InlineData("480p")]
    [InlineData("720p")]
    [InlineData("1080p")]
    [InlineData("1080i")]
    [InlineData("2160p")]
    [InlineData("4k")]
    [InlineData("8K")]
    [InlineData("UHD")]
    public void Resolution_Matches(string token) =>
        StringExtensions.MatchResolutionTag().IsMatch(token).Should().BeTrue();

    [Theory]
    [InlineData("WEB-DL")]
    [InlineData("WEBRip")]
    [InlineData("BluRay")]
    [InlineData("BDRip")]
    [InlineData("HDTV")]
    [InlineData("DVDRip")]
    [InlineData("REMUX")]
    [InlineData("AMZN")]
    [InlineData("DSNP")]
    [InlineData("HMAX")]
    [InlineData("NFLX")]
    [InlineData("HDCAM")]
    public void Source_Matches(string token) =>
        StringExtensions.MatchSourceTag().IsMatch(token).Should().BeTrue();

    [Theory]
    [InlineData("x264")]
    [InlineData("x265")]
    [InlineData("H.264")]
    [InlineData("H265")]
    [InlineData("HEVC")]
    [InlineData("XviD")]
    [InlineData("DivX")]
    [InlineData("AVC")]
    [InlineData("VC-1")]
    [InlineData("VP9")]
    public void Codec_Matches(string token) =>
        StringExtensions.MatchCodecTag().IsMatch(token).Should().BeTrue();

    [Theory]
    [InlineData("DDP5.1")]
    [InlineData("DD5.1")]
    [InlineData("EAC3")]
    [InlineData("AC3")]
    [InlineData("DTS")]
    [InlineData("DTS-HD")]
    [InlineData("TrueHD")]
    [InlineData("Atmos")]
    [InlineData("AAC")]
    [InlineData("AAC2.0")]
    [InlineData("FLAC")]
    [InlineData("MP3")]
    public void Audio_Matches(string token) =>
        StringExtensions.MatchAudioTag().IsMatch(token).Should().BeTrue();

    [Theory]
    [InlineData("10bit")]
    [InlineData("8bit")]
    [InlineData("HDR")]
    [InlineData("HDR10")]
    [InlineData("HDR10+")]
    [InlineData("DoVi")]
    [InlineData("Dolby Vision")]
    [InlineData("HLG")]
    public void Hdr_Matches(string token) =>
        StringExtensions.MatchHdrTag().IsMatch(token).Should().BeTrue();

    [Theory]
    [InlineData("REPACK")]
    [InlineData("MULTI")]
    [InlineData("IMAX")]
    public void Flag_Matches(string token) =>
        StringExtensions.MatchFlagTag().IsMatch(token).Should().BeTrue();

    // Real title words that merely contain a token substring must never match.
    [Theory]
    [InlineData("Limitless")]
    [InlineData("Therapy")]
    [InlineData("Website")]
    [InlineData("Account")]
    [InlineData("Multitude")]
    public void TitleWords_AreNotTags(string word)
    {
        StringExtensions.MatchResolutionTag().IsMatch(word).Should().BeFalse();
        StringExtensions.MatchSourceTag().IsMatch(word).Should().BeFalse();
        StringExtensions.MatchCodecTag().IsMatch(word).Should().BeFalse();
        StringExtensions.MatchAudioTag().IsMatch(word).Should().BeFalse();
        StringExtensions.MatchHdrTag().IsMatch(word).Should().BeFalse();
        StringExtensions.MatchFlagTag().IsMatch(word).Should().BeFalse();
    }

    [Theory]
    [InlineData(["The Office 1080p WEB-DL", StringExtensions.ReleaseTagCategory.Resolution, "1080p"])]
    [InlineData(["Some Show WEB-DL x265", StringExtensions.ReleaseTagCategory.Source, "WEB-DL"])]
    [InlineData(["Movie Title HEVC", StringExtensions.ReleaseTagCategory.Codec, "HEVC"])]
    [InlineData(["Concert EAC3", StringExtensions.ReleaseTagCategory.Audio, "EAC3"])]
    [InlineData(["Film HDR10", StringExtensions.ReleaseTagCategory.Hdr, "HDR10"])]
    [InlineData(["Feature IMAX", StringExtensions.ReleaseTagCategory.Flag, "IMAX"])]
    public void TryGetReleaseTag_ReportsEarliestCategory(
        string input, StringExtensions.ReleaseTagCategory expectedCategory, string expectedValue)
    {
        bool found = input.TryGetReleaseTag(out string value, out StringExtensions.ReleaseTagCategory category);

        found.Should().BeTrue();
        category.Should().Be(expectedCategory);
        value.Should().BeEquivalentTo(expectedValue);
    }

    [Fact]
    public void TryGetReleaseTag_ReturnsFalse_ForCleanTitle()
    {
        "The West Wing".TryGetReleaseTag(out _, out _).Should().BeFalse();
    }
}
