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
/// Covers the expanded scene vocabulary (broadcast/screener sources, AV1/VVC/H.266
/// codecs, LPCM/DD+ audio, SDR, and scene process flags). Crucially it also pins
/// the over-strip guards: only tokens that are never used as leading title words
/// were added, so titles like "Internal Affairs" or "Opus" survive intact.
/// </summary>
public class ReleaseTagVocabularyTests
{
    [Theory]
    [InlineData(data: "SDTV")]
    [InlineData(data: "DVB")]
    [InlineData(data: "VODRip")]
    [InlineData(data: "TVRip")]
    [InlineData(data: "SATRip")]
    [InlineData(data: "WEBCap")]
    [InlineData(data: "HDTS")]
    [InlineData(data: "TELESYNC")]
    [InlineData(data: "TELECINE")]
    [InlineData(data: "WORKPRINT")]
    [InlineData(data: "PPV")]
    [InlineData(data: "SCREENER")]
    public void NewSourceTokens_Match(string token) =>
        StringExtensions.MatchSourceTag().IsMatch(input: token).Should().BeTrue();

    [Theory]
    [InlineData(data: "AV1")]
    [InlineData(data: "VVC")]
    [InlineData(data: "H266")]
    [InlineData(data: "H.266")]
    [InlineData(data: "MPEG4")]
    public void NewCodecTokens_Match(string token) =>
        StringExtensions.MatchCodecTag().IsMatch(input: token).Should().BeTrue();

    [Theory]
    [InlineData(data: "LPCM")]
    [InlineData(data: "DD+")]
    [InlineData(data: "DDP+")]
    public void NewAudioTokens_Match(string token) =>
        StringExtensions.MatchAudioTag().IsMatch(input: token).Should().BeTrue();

    [Fact]
    public void Sdr_Matches() =>
        StringExtensions.MatchHdrTag().IsMatch(input: "SDR").Should().BeTrue();

    [Theory]
    [InlineData(data: "DIRFIX")]
    [InlineData(data: "NFOFIX")]
    [InlineData(data: "READNFO")]
    [InlineData(data: "PROOFFIX")]
    [InlineData(data: "RERIP")]
    public void NewFlagTokens_Match(string token) =>
        StringExtensions.MatchFlagTag().IsMatch(input: token).Should().BeTrue();

    [Theory]
    [InlineData(data: ["The Bureau SDTV", "The Bureau"])]
    [InlineData(data: ["Some Doc SCREENER", "Some Doc"])]
    [InlineData(data: ["Planet Earth WORKPRINT", "Planet Earth"])]
    [InlineData(data: ["Short Film AV1", "Short Film"])]
    [InlineData(data: ["Old Clip MPEG4", "Old Clip"])]
    [InlineData(data: ["Concert LPCM", "Concert"])]
    [InlineData(data: ["The Mix DD+", "The Mix"])]
    [InlineData(data: ["Demo Scene SDR", "Demo Scene"])]
    [InlineData(data: ["Some Release DIRFIX", "Some Release"])]
    [InlineData(data: ["The File NFOFIX", "The File"])]
    public void CleanReleaseTitle_StripsNewTokens(string raw, string expected) =>
        raw.CleanReleaseTitle().Should().Be(expected: expected);

    // Over-strip guards: none of these contain a real tag, so they must be returned
    // verbatim. They specifically exercise words we deliberately did NOT add.
    [Theory]
    [InlineData(data: "Internal Affairs")]
    [InlineData(data: "Opus")]
    [InlineData(data: "Extended Family")]
    [InlineData(data: "The Complete Angler")]
    [InlineData(data: "Without a Trace")]
    [InlineData(data: "Dual Survival")]
    public void CleanReleaseTitle_DoesNotOverStripTitleWords(string title) =>
        title.CleanReleaseTitle().Should().Be(expected: title);
}
