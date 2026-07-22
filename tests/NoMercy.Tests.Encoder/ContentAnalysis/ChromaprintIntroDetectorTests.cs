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

using NoMercy.Encoder.ContentAnalysis.Fingerprinting;

namespace NoMercy.Tests.Encoder.ContentAnalysis;

/// <summary>
/// Covers the intro-detection algorithm with synthetic fingerprints — the
/// chromaprint invocation itself is a separate concern. Each test stages
/// a situation (identical intros, divergent intros, partial overlap) and
/// asserts the returned <see cref="IntroMarker"/> lines up with the
/// constructed overlap.
/// </summary>
public class ChromaprintIntroDetectorTests
{
    private static readonly TimeSpan FrameDuration = TimeSpan.FromMilliseconds(milliseconds: 186);

    [Fact]
    public void Detect_IdenticalFingerprints_ReturnsFullWindowMarker()
    {
        uint[] hashes = Enumerable.Range(start: 1, count: 200).Select(selector: i => (uint)(i * 7919)).ToArray();
        AudioFingerprint a = Print(hashes: hashes);
        AudioFingerprint b = Print(hashes: hashes);

        IntroMarker? marker = new ChromaprintIntroDetector().DetectIntro(episodeFingerprints: [a, b]);

        marker.Should().NotBeNull();
        marker!.Duration.Should().BeGreaterThan(expected: TimeSpan.Zero);
        marker.Confidence.Should().BeGreaterThan(expected: 0.9);
    }

    [Fact]
    public void Detect_SharedIntroDifferentSuffix_FindsIntroOnly()
    {
        // Frames 0..79 identical (the intro), then content diverges.
        uint[] intro = Enumerable.Range(start: 1, count: 80).Select(selector: i => (uint)(i * 65537)).ToArray();
        uint[] showA = intro.Concat(second: RandomBlock(count: 120, seed: 1)).ToArray();
        uint[] showB = intro.Concat(second: RandomBlock(count: 120, seed: 2)).ToArray();

        IntroMarker? marker = new ChromaprintIntroDetector().DetectIntro(episodeFingerprints:
        [
            Print(hashes: showA),
            Print(hashes: showB),
        ]);

        marker.Should().NotBeNull();
        // Detected run must cover the shared intro frames (±a few for
        // threshold fuzz). Frame 0 → frame ~80 of reference print.
        marker!.Start.Should().BeCloseTo(nearbyTime: TimeSpan.Zero, precision: TimeSpan.FromMilliseconds(milliseconds: 200));
        int detectedFrames = (int)(
            marker.Duration.TotalMilliseconds / FrameDuration.TotalMilliseconds
        );
        detectedFrames.Should().BeInRange(minimumValue: 70, maximumValue: 85);
    }

    [Fact]
    public void Detect_NoSharedRegion_ReturnsNull()
    {
        AudioFingerprint a = Print(hashes: RandomBlock(count: 200, seed: 1));
        AudioFingerprint b = Print(hashes: RandomBlock(count: 200, seed: 2));

        IntroMarker? marker = new ChromaprintIntroDetector().DetectIntro(episodeFingerprints: [a, b]);

        marker.Should().BeNull();
    }

    [Fact]
    public void Detect_SingleEpisode_ReturnsNull()
    {
        AudioFingerprint only = Print(hashes: RandomBlock(count: 200, seed: 1));

        new ChromaprintIntroDetector().DetectIntro(episodeFingerprints: [only]).Should().BeNull();
    }

    [Fact]
    public void Detect_EmptyFingerprintInList_ReturnsNull()
    {
        AudioFingerprint real = Print(hashes: RandomBlock(count: 200, seed: 1));
        AudioFingerprint empty = Print(hashes: []);

        new ChromaprintIntroDetector().DetectIntro(episodeFingerprints: [real, empty]).Should().BeNull();
    }

    [Fact]
    public void Detect_ThreeEpisodesSharedIntro_AllAgree()
    {
        uint[] intro = Enumerable.Range(start: 1, count: 100).Select(selector: i => (uint)(i * 2654435761)).ToArray();
        uint[] ep1 = intro.Concat(second: RandomBlock(count: 100, seed: 1)).ToArray();
        uint[] ep2 = intro.Concat(second: RandomBlock(count: 100, seed: 2)).ToArray();
        uint[] ep3 = intro.Concat(second: RandomBlock(count: 100, seed: 3)).ToArray();

        IntroMarker? marker = new ChromaprintIntroDetector().DetectIntro(episodeFingerprints:
        [
            Print(hashes: ep1),
            Print(hashes: ep2),
            Print(hashes: ep3),
        ]);

        marker.Should().NotBeNull();
        int detectedFrames = (int)(
            marker!.Duration.TotalMilliseconds / FrameDuration.TotalMilliseconds
        );
        detectedFrames.Should().BeInRange(minimumValue: 90, maximumValue: 105);
    }

    [Fact]
    public void Detect_IntroShiftedOffsetInOneEpisode_StillMatches()
    {
        uint[] intro = Enumerable.Range(start: 1, count: 80).Select(selector: i => (uint)(i * 3_141_592)).ToArray();
        uint[] ep1 = intro.Concat(second: RandomBlock(count: 120, seed: 1)).ToArray();
        // ep2's intro starts 20 frames later (common in real content —
        // network idents / recaps shift the intro offset).
        uint[] ep2 = RandomBlock(count: 20, seed: 99)
            .Concat(second: intro)
            .Concat(second: RandomBlock(count: 100, seed: 2))
            .ToArray();

        IntroMarker? marker = new ChromaprintIntroDetector().DetectIntro(episodeFingerprints: [Print(hashes: ep1), Print(hashes: ep2)]);

        marker.Should().NotBeNull();
        int detectedFrames = (int)(
            marker!.Duration.TotalMilliseconds / FrameDuration.TotalMilliseconds
        );
        detectedFrames.Should().BeGreaterThan(expected: 70);
    }

    [Fact]
    public void Detect_OutroRegion_StartTimePreserved()
    {
        // Fingerprint taken from minute 25 of a 27-minute show — outro
        // marker times should be expressed in source-file coordinates,
        // not relative to the fingerprint window.
        uint[] outro = Enumerable.Range(start: 1, count: 100).Select(selector: i => (uint)(i * 11_411)).ToArray();
        uint[] ep1 = RandomBlock(count: 80, seed: 11).Concat(second: outro).ToArray();
        uint[] ep2 = RandomBlock(count: 80, seed: 12).Concat(second: outro).ToArray();

        AudioFingerprint p1 = new(Hashes: ep1, FrameDuration: FrameDuration, StartTime: TimeSpan.FromMinutes(minutes: 25));
        AudioFingerprint p2 = new(Hashes: ep2, FrameDuration: FrameDuration, StartTime: TimeSpan.FromMinutes(minutes: 25));

        IntroMarker? marker = new ChromaprintIntroDetector().DetectOutro(episodeFingerprints: [p1, p2]);

        marker.Should().NotBeNull();
        // Detected marker must be in absolute source time (>25 min), not
        // near zero.
        marker!.Start.Should().BeGreaterThan(expected: TimeSpan.FromMinutes(minutes: 25));
    }

    [Fact]
    public void BestAlignment_IdenticalInputs_ReturnsFullLength()
    {
        uint[] hashes = Enumerable.Range(start: 1, count: 50).Select(selector: i => (uint)(i * 9_973)).ToArray();
        ChromaprintIntroDetector detector = new();

        (int aStart, int bStart, int length) = detector.BestAlignment(a: hashes, b: hashes);

        aStart.Should().Be(expected: 0);
        bStart.Should().Be(expected: 0);
        length.Should().Be(expected: 50);
    }

    [Fact]
    public void BestAlignment_DisjointInputs_LengthBelowThreshold()
    {
        uint[] a = RandomBlock(count: 50, seed: 1);
        uint[] b = RandomBlock(count: 50, seed: 2);
        ChromaprintIntroDetector detector = new();

        (_, _, int length) = detector.BestAlignment(a: a, b: b);

        // Pseudo-random independent sequences produce tiny coincidental
        // matches only; certainly not a 25+ frame run.
        length.Should().BeLessThan(expected: 25);
    }

    private static AudioFingerprint Print(uint[] hashes) =>
        new(Hashes: hashes, FrameDuration: FrameDuration, StartTime: TimeSpan.Zero);

    private static uint[] RandomBlock(int count, int seed)
    {
        Random rng = new(Seed: seed);
        uint[] arr = new uint[count];
        for (int i = 0; i < count; i++)
            arr[i] = (uint)rng.Next();
        return arr;
    }
}
