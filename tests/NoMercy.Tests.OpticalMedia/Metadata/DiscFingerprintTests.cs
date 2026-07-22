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

using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Sources;

namespace NoMercy.Tests.OpticalMedia.Metadata;

/// <summary>
/// REQUIREMENT: <see cref="NoMercy.OpticalMedia.Metadata.DiscFingerprint"/>
/// must produce a stable per-disc identifier derived only from title count
/// and per-title durations (rounded to whole seconds) — never from labels,
/// drive paths, or read order — so re-inserting the same physical disc, on
/// any drive, on any machine, yields the identical fingerprint. Two discs
/// that differ in even one title's duration must fingerprint differently.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class DiscFingerprintTests
{
    private static DiscTitle MakeTitle(int index, TimeSpan duration) =>
        new(
            Index: index,
            Name: $"Title {index}",
            Duration: duration,
            VideoStreams: [],
            AudioStreams: [],
            Subtitles: [],
            Chapters: [],
            EstimatedSizeBytes: 0,
            IsMainFeature: index == 0
        );

    private static DiscInfo MakeDisc(params (int Index, TimeSpan Duration)[] titles) =>
        new(
            Type: OpticalDiscType.Dvd,
            DiscLabel: "TEST",
            Titles: titles.Select(selector: t => MakeTitle(index: t.Index, duration: t.Duration)).ToArray(),
            AudioTracks: null,
            TotalDuration: TimeSpan.FromSeconds(value: titles.Sum(selector: t => t.Duration.TotalSeconds))
        );

    [Fact]
    public void Compute_NoTitles_ReturnsEmptyString()
    {
        DiscInfo disc = MakeDisc();

        string fingerprint = NoMercy.OpticalMedia.Metadata.DiscFingerprint.Compute(info: disc);

        fingerprint.Should().BeEmpty();
    }

    [Fact]
    public void Compute_SameDiscTwice_ProducesIdenticalFingerprint()
    {
        DiscInfo discA = MakeDisc(titles: [(0, TimeSpan.FromMinutes(minutes: 120)), (1, TimeSpan.FromMinutes(minutes: 5))]);
        DiscInfo discB = MakeDisc(titles: [(0, TimeSpan.FromMinutes(minutes: 120)), (1, TimeSpan.FromMinutes(minutes: 5))]);

        string fingerprintA = NoMercy.OpticalMedia.Metadata.DiscFingerprint.Compute(info: discA);
        string fingerprintB = NoMercy.OpticalMedia.Metadata.DiscFingerprint.Compute(info: discB);

        fingerprintA.Should().Be(expected: fingerprintB);
    }

    [Fact]
    public void Compute_IgnoresDiscLabelAndDriveMetadata()
    {
        // Two discs with identical titles but different labels must
        // fingerprint identically — the label is not physical disc content.
        DiscInfo discA = MakeDisc(titles: (0, TimeSpan.FromMinutes(minutes: 90))) with
        {
            DiscLabel = "COPY_A",
        };
        DiscInfo discB = MakeDisc(titles: (0, TimeSpan.FromMinutes(minutes: 90))) with { DiscLabel = "COPY_B" };

        string fingerprintA = NoMercy.OpticalMedia.Metadata.DiscFingerprint.Compute(info: discA);
        string fingerprintB = NoMercy.OpticalMedia.Metadata.DiscFingerprint.Compute(info: discB);

        fingerprintA.Should().Be(expected: fingerprintB);
    }

    [Fact]
    public void Compute_DifferentTitleDuration_ProducesDifferentFingerprint()
    {
        DiscInfo discA = MakeDisc(titles: (0, TimeSpan.FromMinutes(minutes: 90)));
        DiscInfo discB = MakeDisc(titles: (0, TimeSpan.FromMinutes(minutes: 91)));

        string fingerprintA = NoMercy.OpticalMedia.Metadata.DiscFingerprint.Compute(info: discA);
        string fingerprintB = NoMercy.OpticalMedia.Metadata.DiscFingerprint.Compute(info: discB);

        fingerprintA.Should().NotBe(unexpected: fingerprintB);
    }

    [Fact]
    public void Compute_DifferentTitleCount_ProducesDifferentFingerprint()
    {
        DiscInfo discA = MakeDisc(titles: (0, TimeSpan.FromMinutes(minutes: 90)));
        DiscInfo discB = MakeDisc(titles: [(0, TimeSpan.FromMinutes(minutes: 90)), (1, TimeSpan.FromMinutes(minutes: 5))]);

        string fingerprintA = NoMercy.OpticalMedia.Metadata.DiscFingerprint.Compute(info: discA);
        string fingerprintB = NoMercy.OpticalMedia.Metadata.DiscFingerprint.Compute(info: discB);

        fingerprintA.Should().NotBe(unexpected: fingerprintB);
    }

    [Fact]
    public void Compute_TitleOrderInArray_DoesNotAffectFingerprint()
    {
        // Compute() sorts by Index before hashing — array insertion order
        // must not matter, only the logical title index does.
        DiscInfo discA = MakeDisc(titles: [(0, TimeSpan.FromMinutes(minutes: 90)), (1, TimeSpan.FromMinutes(minutes: 5))]);
        DiscInfo discB = MakeDisc(titles: [(1, TimeSpan.FromMinutes(minutes: 5)), (0, TimeSpan.FromMinutes(minutes: 90))]);

        string fingerprintA = NoMercy.OpticalMedia.Metadata.DiscFingerprint.Compute(info: discA);
        string fingerprintB = NoMercy.OpticalMedia.Metadata.DiscFingerprint.Compute(info: discB);

        fingerprintA.Should().Be(expected: fingerprintB);
    }

    [Fact]
    public void Compute_ReturnsUppercaseHex()
    {
        DiscInfo disc = MakeDisc(titles: (0, TimeSpan.FromMinutes(minutes: 90)));

        string fingerprint = NoMercy.OpticalMedia.Metadata.DiscFingerprint.Compute(info: disc);

        fingerprint.Should().MatchRegex(regularExpression: "^[0-9A-F]+$");
        fingerprint.Should().HaveLength(expected: 40, because: "SHA1 hex-encoded is 40 characters");
    }

    [Fact]
    public void Compute_SubSecondDurationDifference_RoundsToSameFingerprint()
    {
        // Compute() truncates to whole seconds via (long)TotalSeconds —
        // sub-second jitter across re-inserts/re-reads must not change it.
        DiscInfo discA = MakeDisc(titles: (0, TimeSpan.FromSeconds(value: 5400.1)));
        DiscInfo discB = MakeDisc(titles: (0, TimeSpan.FromSeconds(value: 5400.9)));

        string fingerprintA = NoMercy.OpticalMedia.Metadata.DiscFingerprint.Compute(info: discA);
        string fingerprintB = NoMercy.OpticalMedia.Metadata.DiscFingerprint.Compute(info: discB);

        fingerprintA.Should().Be(expected: fingerprintB);
    }
}
