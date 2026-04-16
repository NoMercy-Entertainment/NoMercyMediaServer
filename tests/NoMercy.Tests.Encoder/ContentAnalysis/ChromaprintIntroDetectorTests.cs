namespace NoMercy.Tests.Encoder.ContentAnalysis;

using NoMercy.Encoder.ContentAnalysis.Fingerprinting;

/// <summary>
/// Covers the intro-detection algorithm with synthetic fingerprints — the
/// chromaprint invocation itself is a separate concern. Each test stages
/// a situation (identical intros, divergent intros, partial overlap) and
/// asserts the returned <see cref="IntroMarker"/> lines up with the
/// constructed overlap.
/// </summary>
public class ChromaprintIntroDetectorTests
{
    private static readonly TimeSpan FrameDuration = TimeSpan.FromMilliseconds(186);

    [Fact]
    public void Detect_IdenticalFingerprints_ReturnsFullWindowMarker()
    {
        uint[] hashes = Enumerable.Range(1, 200).Select(i => (uint)(i * 7919)).ToArray();
        AudioFingerprint a = Print(hashes);
        AudioFingerprint b = Print(hashes);

        IntroMarker? marker = new ChromaprintIntroDetector().DetectIntro([a, b]);

        marker.Should().NotBeNull();
        marker!.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        marker.Confidence.Should().BeGreaterThan(0.9);
    }

    [Fact]
    public void Detect_SharedIntroDifferentSuffix_FindsIntroOnly()
    {
        // Frames 0..79 identical (the intro), then content diverges.
        uint[] intro = Enumerable.Range(1, 80).Select(i => (uint)(i * 65537)).ToArray();
        uint[] showA = intro.Concat(RandomBlock(120, seed: 1)).ToArray();
        uint[] showB = intro.Concat(RandomBlock(120, seed: 2)).ToArray();

        IntroMarker? marker = new ChromaprintIntroDetector().DetectIntro([
            Print(showA),
            Print(showB),
        ]);

        marker.Should().NotBeNull();
        // Detected run must cover the shared intro frames (±a few for
        // threshold fuzz). Frame 0 → frame ~80 of reference print.
        marker!.Start.Should().BeCloseTo(TimeSpan.Zero, TimeSpan.FromMilliseconds(200));
        int detectedFrames = (int)(
            marker.Duration.TotalMilliseconds / FrameDuration.TotalMilliseconds
        );
        detectedFrames.Should().BeInRange(70, 85);
    }

    [Fact]
    public void Detect_NoSharedRegion_ReturnsNull()
    {
        AudioFingerprint a = Print(RandomBlock(200, seed: 1));
        AudioFingerprint b = Print(RandomBlock(200, seed: 2));

        IntroMarker? marker = new ChromaprintIntroDetector().DetectIntro([a, b]);

        marker.Should().BeNull();
    }

    [Fact]
    public void Detect_SingleEpisode_ReturnsNull()
    {
        AudioFingerprint only = Print(RandomBlock(200, seed: 1));

        new ChromaprintIntroDetector().DetectIntro([only]).Should().BeNull();
    }

    [Fact]
    public void Detect_EmptyFingerprintInList_ReturnsNull()
    {
        AudioFingerprint real = Print(RandomBlock(200, seed: 1));
        AudioFingerprint empty = Print([]);

        new ChromaprintIntroDetector().DetectIntro([real, empty]).Should().BeNull();
    }

    [Fact]
    public void Detect_ThreeEpisodesSharedIntro_AllAgree()
    {
        uint[] intro = Enumerable.Range(1, 100).Select(i => (uint)(i * 2654435761)).ToArray();
        uint[] ep1 = intro.Concat(RandomBlock(100, seed: 1)).ToArray();
        uint[] ep2 = intro.Concat(RandomBlock(100, seed: 2)).ToArray();
        uint[] ep3 = intro.Concat(RandomBlock(100, seed: 3)).ToArray();

        IntroMarker? marker = new ChromaprintIntroDetector().DetectIntro([
            Print(ep1),
            Print(ep2),
            Print(ep3),
        ]);

        marker.Should().NotBeNull();
        int detectedFrames = (int)(
            marker!.Duration.TotalMilliseconds / FrameDuration.TotalMilliseconds
        );
        detectedFrames.Should().BeInRange(90, 105);
    }

    [Fact]
    public void Detect_IntroShiftedOffsetInOneEpisode_StillMatches()
    {
        uint[] intro = Enumerable.Range(1, 80).Select(i => (uint)(i * 3_141_592)).ToArray();
        uint[] ep1 = intro.Concat(RandomBlock(120, seed: 1)).ToArray();
        // ep2's intro starts 20 frames later (common in real content —
        // network idents / recaps shift the intro offset).
        uint[] ep2 = RandomBlock(20, seed: 99)
            .Concat(intro)
            .Concat(RandomBlock(100, seed: 2))
            .ToArray();

        IntroMarker? marker = new ChromaprintIntroDetector().DetectIntro([Print(ep1), Print(ep2)]);

        marker.Should().NotBeNull();
        int detectedFrames = (int)(
            marker!.Duration.TotalMilliseconds / FrameDuration.TotalMilliseconds
        );
        detectedFrames.Should().BeGreaterThan(70);
    }

    [Fact]
    public void Detect_OutroRegion_StartTimePreserved()
    {
        // Fingerprint taken from minute 25 of a 27-minute show — outro
        // marker times should be expressed in source-file coordinates,
        // not relative to the fingerprint window.
        uint[] outro = Enumerable.Range(1, 100).Select(i => (uint)(i * 11_411)).ToArray();
        uint[] ep1 = RandomBlock(80, seed: 11).Concat(outro).ToArray();
        uint[] ep2 = RandomBlock(80, seed: 12).Concat(outro).ToArray();

        AudioFingerprint p1 = new(ep1, FrameDuration, TimeSpan.FromMinutes(25));
        AudioFingerprint p2 = new(ep2, FrameDuration, TimeSpan.FromMinutes(25));

        IntroMarker? marker = new ChromaprintIntroDetector().DetectOutro([p1, p2]);

        marker.Should().NotBeNull();
        // Detected marker must be in absolute source time (>25 min), not
        // near zero.
        marker!.Start.Should().BeGreaterThan(TimeSpan.FromMinutes(25));
    }

    [Fact]
    public void BestAlignment_IdenticalInputs_ReturnsFullLength()
    {
        uint[] hashes = Enumerable.Range(1, 50).Select(i => (uint)(i * 9_973)).ToArray();
        ChromaprintIntroDetector detector = new();

        (int aStart, int bStart, int length) = detector.BestAlignment(hashes, hashes);

        aStart.Should().Be(0);
        bStart.Should().Be(0);
        length.Should().Be(50);
    }

    [Fact]
    public void BestAlignment_DisjointInputs_LengthBelowThreshold()
    {
        uint[] a = RandomBlock(50, seed: 1);
        uint[] b = RandomBlock(50, seed: 2);
        ChromaprintIntroDetector detector = new();

        (_, _, int length) = detector.BestAlignment(a, b);

        // Pseudo-random independent sequences produce tiny coincidental
        // matches only; certainly not a 25+ frame run.
        length.Should().BeLessThan(25);
    }

    private static AudioFingerprint Print(uint[] hashes) =>
        new(hashes, FrameDuration, TimeSpan.Zero);

    private static uint[] RandomBlock(int count, int seed)
    {
        Random rng = new(seed);
        uint[] arr = new uint[count];
        for (int i = 0; i < count; i++)
            arr[i] = (uint)rng.Next();
        return arr;
    }
}
