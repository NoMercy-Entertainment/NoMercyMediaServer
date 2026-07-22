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

[Trait(name: "Category", value: "Unit")]
public class DiscIdAlgorithmTests
{
    // ── MusicBrainzDiscId.Compute — algorithm correctness ─────────────────

    /// <summary>
    /// Canonical worked example from the MusicBrainz Disc ID Calculation spec
    /// (https://musicbrainz.org/doc/Disc_ID_Calculation): a 6-track disc whose
    /// absolute frame offsets and lead-out produce a known disc id. Offsets are
    /// absolute (track 1 = 150), exactly as the algorithm hashes them. This is
    /// the only test that proves the hash is byte-correct — a wrong pre-gap or
    /// hex width would change the result.
    /// </summary>
    [Fact]
    public void Compute_CanonicalSpecFixture_ReturnsExpectedDiscId()
    {
        DiscToc toc = new(
            FirstTrack: 1,
            LastTrack: 6,
            LeadOutOffsetSectors: 95462,
            TrackOffsetsSectors: [150, 15363, 32314, 46592, 63414, 80489]
        );

        string discId = MusicBrainzDiscId.Compute(toc: toc);

        discId.Should().Be(expected: "49HHV7Eb8UKF3aQiNmu1GR8vKTY-");
    }

    [Fact]
    public void Compute_AlwaysProduces28CharSubstitutedId()
    {
        DiscToc toc = new(
            FirstTrack: 1,
            LastTrack: 6,
            LeadOutOffsetSectors: 95462,
            TrackOffsetsSectors: [150, 15363, 32314, 46592, 63414, 80489]
        );

        string discId = MusicBrainzDiscId.Compute(toc: toc);

        discId.Should().HaveLength(expected: 28, because: "MusicBrainz disc IDs are always 28 chars");
        discId.Should().MatchRegex(regularExpression: @"^[A-Za-z0-9._-]+$", because: "base64url-like alphabet only");
        discId.Should().NotContainAny(values: ["+", "/", "="]);
    }

    [Fact]
    public void Compute_SameToc_IsDeterministic()
    {
        DiscToc toc = new(
            FirstTrack: 1,
            LastTrack: 3,
            LeadOutOffsetSectors: 60150,
            TrackOffsetsSectors: [150, 15150, 30150]
        );

        MusicBrainzDiscId.Compute(toc: toc).Should().Be(expected: MusicBrainzDiscId.Compute(toc: toc));
    }

    [Fact]
    public void Compute_DifferentLeadOut_ProducesDifferentId()
    {
        DiscToc tocA = new(FirstTrack: 1, LastTrack: 1, LeadOutOffsetSectors: 18150, TrackOffsetsSectors: [150]);
        DiscToc tocB = new(FirstTrack: 1, LastTrack: 1, LeadOutOffsetSectors: 19150, TrackOffsetsSectors: [150]);

        MusicBrainzDiscId.Compute(toc: tocA).Should().NotBe(unexpected: MusicBrainzDiscId.Compute(toc: tocB));
    }

    [Fact]
    public void Compute_MismatchedTrackCount_Throws()
    {
        DiscToc toc = new(
            FirstTrack: 1,
            LastTrack: 3,
            LeadOutOffsetSectors: 60150,
            TrackOffsetsSectors: [150, 15150]
        );

        Action act = () => MusicBrainzDiscId.Compute(toc: toc);
        act.Should().Throw<ArgumentException>();
    }

    // ── NullTocReader ─────────────────────────────────────────────────────

    [Fact]
    public async Task NullTocReader_AlwaysReturnsNull()
    {
        NullTocReader reader = new();
        DiscToc? result = await reader.ReadTocAsync(drivePath: "/dev/sr0", ct: CancellationToken.None);
        result.Should().BeNull();
    }
}
