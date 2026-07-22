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

using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Reconciliation;

namespace NoMercy.Tests.Encoder.Reconciliation;

public class ProfileFingerprintTests
{
    [Fact]
    public void Compute_SameResolvedProfile_ProducesTheSameHash()
    {
        Ulid id = Ulid.NewUlid();
        EncodingProfile first = Build(id: id, segmentDurationSeconds: 6);
        EncodingProfile second = Build(id: id, segmentDurationSeconds: 6);

        ProfileFingerprint.Compute(profile: first).Should().Be(expected: ProfileFingerprint.Compute(profile: second));
    }

    [Fact]
    public void Compute_ChangedSetting_ProducesADifferentHash()
    {
        Ulid id = Ulid.NewUlid();
        EncodingProfile original = Build(id: id, segmentDurationSeconds: 6);
        EncodingProfile editedInPlace = Build(id: id, segmentDurationSeconds: 4);

        ProfileFingerprint
            .Compute(profile: original)
            .Should()
            .NotBe(
                unexpected: ProfileFingerprint.Compute(profile: editedInPlace),
                because: "a preset edited in place keeps its id — the fingerprint, not the id, must catch the change"
            );
    }

    [Fact]
    public void Compute_ReturnsALowercaseHexSha256()
    {
        string fingerprint = ProfileFingerprint.Compute(profile: Build(id: Ulid.NewUlid(), segmentDurationSeconds: 6));

        fingerprint.Should().MatchRegex(regularExpression: "^[0-9a-f]{64}$");
    }

    private static EncodingProfile Build(Ulid id, int segmentDurationSeconds) =>
        new(
            Id: id,
            Name: "test-profile",
            Container: Container.HlsFmp4,
            Video: null,
            Audio: [],
            Subtitles: [],
            SegmentDurationSeconds: segmentDurationSeconds
        );
}
