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

[Trait("Category", "Unit")]
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
            1,
            6,
            95462,
            [150, 15363, 32314, 46592, 63414, 80489]
        );

        string discId = MusicBrainzDiscId.Compute(toc);

        discId.Should().Be("49HHV7Eb8UKF3aQiNmu1GR8vKTY-");
    }

    [Fact]
    public void Compute_AlwaysProduces28CharSubstitutedId()
    {
        DiscToc toc = new(
            1,
            6,
            95462,
            [150, 15363, 32314, 46592, 63414, 80489]
        );

        string discId = MusicBrainzDiscId.Compute(toc);

        discId.Should().HaveLength(28, "MusicBrainz disc IDs are always 28 chars");
        discId.Should().MatchRegex(@"^[A-Za-z0-9._-]+$", "base64url-like alphabet only");
        discId.Should().NotContainAny(["+", "/", "="]);
    }

    [Fact]
    public void Compute_SameToc_IsDeterministic()
    {
        DiscToc toc = new(
            1,
            3,
            60150,
            [150, 15150, 30150]
        );

        MusicBrainzDiscId.Compute(toc).Should().Be(MusicBrainzDiscId.Compute(toc));
    }

    [Fact]
    public void Compute_DifferentLeadOut_ProducesDifferentId()
    {
        DiscToc tocA = new(1, 1, 18150, [150]);
        DiscToc tocB = new(1, 1, 19150, [150]);

        MusicBrainzDiscId.Compute(tocA).Should().NotBe(MusicBrainzDiscId.Compute(tocB));
    }

    [Fact]
    public void Compute_MismatchedTrackCount_Throws()
    {
        DiscToc toc = new(
            1,
            3,
            60150,
            [150, 15150]
        );

        Action act = () => MusicBrainzDiscId.Compute(toc);
        act.Should().Throw<ArgumentException>();
    }

    // ── NullTocReader ─────────────────────────────────────────────────────

    [Fact]
    public async Task NullTocReader_AlwaysReturnsNull()
    {
        NullTocReader reader = new();
        DiscToc? result = await reader.ReadTocAsync("/dev/sr0", CancellationToken.None);
        result.Should().BeNull();
    }
}
