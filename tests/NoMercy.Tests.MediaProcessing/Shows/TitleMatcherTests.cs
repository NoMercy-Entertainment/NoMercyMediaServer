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
using NoMercy.MediaProcessing.Shows;
using Xunit;

namespace NoMercy.Tests.MediaProcessing.Shows;

public class TitleMatcherTests
{
    [Fact]
    public void Matches_WordOrderReordered_StillMatches()
    {
        // Reproduced against AniList: "SAINT SEIYA: Knights of the Zodiac"
        // (search) vs "Knights of the Zodiac: Saint Seiya" (candidate).
        bool result = TitleMatcher.Matches(
            "SAINT SEIYA: Knights of the Zodiac",
            ["Knights of the Zodiac: Saint Seiya"]
        );

        result.Should().BeTrue();
    }

    [Fact]
    public void Matches_CandidateHasYearSuffix_StillMatches()
    {
        bool result = TitleMatcher.Matches("Fruits Basket", ["Fruits Basket (2019)"]);

        result.Should().BeTrue();
    }

    [Fact]
    public void Matches_CurlyApostropheInCandidate_StillMatches()
    {
        bool result = TitleMatcher.Matches("Journey's End", ["Journey’s End"]);

        result.Should().BeTrue();
    }

    [Fact]
    public void Matches_HyphenatedLocalTitleWithLeadingDash_StillMatches()
    {
        // Re:ZERO -Starting Life in Another World- : the dash must be
        // normalized to a space, not treated as an exclusion token.
        bool result = TitleMatcher.Matches(
            "Re:ZERO -Starting Life in Another World-",
            ["Re:ZERO Starting Life in Another World"]
        );

        result.Should().BeTrue();
    }

    [Fact]
    public void Matches_AniListSynonymsArrayIncluded_StillMatches()
    {
        // AniList's candidate set is Title.Romaji/English/Native + Synonyms;
        // a synonym-only match must still succeed.
        bool result = TitleMatcher.Matches("OP", [null, "One Piece", null, "OP"]);

        result.Should().BeTrue();
    }

    [Fact]
    public void Matches_NoOverlap_ReturnsFalse()
    {
        bool result = TitleMatcher.Matches("Attack on Titan", ["One Piece"]);

        result.Should().BeFalse();
    }

    [Fact]
    public void Matches_EmptySearchTitle_ReturnsFalse()
    {
        bool result = TitleMatcher.Matches("", ["One Piece"]);

        result.Should().BeFalse();
    }
}
