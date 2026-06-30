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
using FluentAssertions;
using NoMercy.NmSystem.Extensions;
using Xunit;

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
    [InlineData("SDTV")]
    [InlineData("DVB")]
    [InlineData("VODRip")]
    [InlineData("TVRip")]
    [InlineData("SATRip")]
    [InlineData("WEBCap")]
    [InlineData("HDTS")]
    [InlineData("TELESYNC")]
    [InlineData("TELECINE")]
    [InlineData("WORKPRINT")]
    [InlineData("PPV")]
    [InlineData("SCREENER")]
    public void NewSourceTokens_Match(string token) =>
        StringExtensions.MatchSourceTag().IsMatch(token).Should().BeTrue();

    [Theory]
    [InlineData("AV1")]
    [InlineData("VVC")]
    [InlineData("H266")]
    [InlineData("H.266")]
    [InlineData("MPEG4")]
    public void NewCodecTokens_Match(string token) =>
        StringExtensions.MatchCodecTag().IsMatch(token).Should().BeTrue();

    [Theory]
    [InlineData("LPCM")]
    [InlineData("DD+")]
    [InlineData("DDP+")]
    public void NewAudioTokens_Match(string token) =>
        StringExtensions.MatchAudioTag().IsMatch(token).Should().BeTrue();

    [Fact]
    public void Sdr_Matches() =>
        StringExtensions.MatchHdrTag().IsMatch("SDR").Should().BeTrue();

    [Theory]
    [InlineData("DIRFIX")]
    [InlineData("NFOFIX")]
    [InlineData("READNFO")]
    [InlineData("PROOFFIX")]
    [InlineData("RERIP")]
    public void NewFlagTokens_Match(string token) =>
        StringExtensions.MatchFlagTag().IsMatch(token).Should().BeTrue();

    [Theory]
    [InlineData("The Bureau SDTV", "The Bureau")]
    [InlineData("Some Doc SCREENER", "Some Doc")]
    [InlineData("Planet Earth WORKPRINT", "Planet Earth")]
    [InlineData("Short Film AV1", "Short Film")]
    [InlineData("Old Clip MPEG4", "Old Clip")]
    [InlineData("Concert LPCM", "Concert")]
    [InlineData("The Mix DD+", "The Mix")]
    [InlineData("Demo Scene SDR", "Demo Scene")]
    [InlineData("Some Release DIRFIX", "Some Release")]
    [InlineData("The File NFOFIX", "The File")]
    public void CleanReleaseTitle_StripsNewTokens(string raw, string expected) =>
        raw.CleanReleaseTitle().Should().Be(expected);

    // Over-strip guards: none of these contain a real tag, so they must be returned
    // verbatim. They specifically exercise words we deliberately did NOT add.
    [Theory]
    [InlineData("Internal Affairs")]
    [InlineData("Opus")]
    [InlineData("Extended Family")]
    [InlineData("The Complete Angler")]
    [InlineData("Without a Trace")]
    [InlineData("Dual Survival")]
    public void CleanReleaseTitle_DoesNotOverStripTitleWords(string title) =>
        title.CleanReleaseTitle().Should().Be(title);
}
