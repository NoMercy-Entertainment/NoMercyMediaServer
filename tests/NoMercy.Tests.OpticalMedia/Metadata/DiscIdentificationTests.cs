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

using NoMercy.OpticalMedia.Metadata;

namespace NoMercy.Tests.OpticalMedia.Metadata;

/// <summary>
/// REQUIREMENT: <see cref="DiscIdentification.TopCandidate"/> must return the
/// first (highest-ranked) candidate when any exist, and null when the
/// candidate list is empty — callers (DiscRipJob's auto-apply / pending-write
/// branches) rely on this to distinguish "no match" from "a ranked match".
/// </summary>
[Trait("Category", "Unit")]
public class DiscIdentificationTests
{
    private static DiscCandidate MakeCandidate(string title, double confidence) =>
        new(
            Source: "tmdb",
            StableId: "1",
            Title: title,
            Year: 2024,
            PosterUrl: null,
            BackdropUrl: null,
            Confidence: confidence
        );

    [Fact]
    public void TopCandidate_EmptyCandidates_ReturnsNull()
    {
        DiscIdentification identification = new(
            Kind: MediaKind.Movie,
            Candidates: [],
            TopConfidence: 0,
            AutoApply: false,
            NeedsManualAssignment: true
        );

        identification.TopCandidate.Should().BeNull();
    }

    [Fact]
    public void TopCandidate_NonEmptyCandidates_ReturnsFirstEntry()
    {
        DiscCandidate first = MakeCandidate("First", 0.95);
        DiscCandidate second = MakeCandidate("Second", 0.5);

        DiscIdentification identification = new(
            Kind: MediaKind.Movie,
            Candidates: [first, second],
            TopConfidence: 0.95,
            AutoApply: true,
            NeedsManualAssignment: false
        );

        identification.TopCandidate.Should().BeSameAs(first);
    }
}
