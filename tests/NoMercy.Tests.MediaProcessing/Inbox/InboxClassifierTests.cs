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

using Moq;
using NoMercy.Database.Models.Libraries;
using NoMercy.MediaProcessing.Inbox;

namespace NoMercy.Tests.MediaProcessing.Inbox;

[Trait(name: "Category", value: "Unit")]
public class InboxClassifierTests
{
    // -----------------------------------------------------------------------
    // Factory helpers
    // -----------------------------------------------------------------------

    private static InboxClassifier MakeClassifier(
        IInboxMetadataProbe? probe = null,
        IInboxAudioTagReader? tagReader = null
    )
    {
        probe ??= new Mock<IInboxMetadataProbe>().Object;
        tagReader ??= new Mock<IInboxAudioTagReader>().Object;
        return new(probe: probe, tagReader: tagReader);
    }

    private static Mock<IInboxMetadataProbe> EmptyProbe()
    {
        Mock<IInboxMetadataProbe> mock = new();
        mock.Setup(expression: p =>
                p.SearchMoviesAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: []);
        mock.Setup(expression: p =>
                p.SearchTvAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: []);
        mock.Setup(expression: p => p.LookupMusicReleaseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (CandidateMatch?)null);
        return mock;
    }

    private static CandidateMatch StrongMovie(string title, int year) =>
        new()
        {
            Provider = "tmdb",
            ExternalId = "123",
            Title = title,
            Year = year,
            Score = 0.85,
        };

    private static CandidateMatch StrongTv(string title, int year) =>
        new()
        {
            Provider = "tmdb",
            ExternalId = "456",
            Title = title,
            Year = year,
            Score = 0.80,
        };

    private static CandidateMatch WeakHit(string title) =>
        new()
        {
            Provider = "tmdb",
            ExternalId = "999",
            Title = title,
            Year = null,
            Score = 0.30,
        };

    private static CandidateMatch MusicCandidate(Guid releaseId, string title) =>
        new()
        {
            Provider = "musicbrainz",
            ExternalId = releaseId.ToString(),
            Title = title,
            Year = 2020,
            Score = 1.0,
        };

    // -----------------------------------------------------------------------
    // MediaFamilyOf — extension split
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(data: ["Inbox/song.flac", "music"])]
    [InlineData(data: ["Inbox/song.mp3", "music"])]
    [InlineData(data: ["Inbox/song.opus", "music"])]
    [InlineData(data: ["Inbox/song.wav", "music"])]
    [InlineData(data: ["Inbox/song.m4a", "music"])]
    public void MediaFamilyOf_AudioExtensions_ReturnMusic(string path, string expected)
    {
        InboxClassifier.MediaFamilyOf(path: path).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["Inbox/movie.mkv", "video"])]
    [InlineData(data: ["Inbox/movie.mp4", "video"])]
    [InlineData(data: ["Inbox/movie.avi", "video"])]
    [InlineData(data: ["Inbox/movie.webm", "video"])]
    [InlineData(data: ["Inbox/movie.mov", "video"])]
    [InlineData(data: ["Inbox/stream.m3u8", "video"])]
    public void MediaFamilyOf_VideoExtensions_ReturnVideo(string path, string expected)
    {
        InboxClassifier.MediaFamilyOf(path: path).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: "Inbox/document.pdf")]
    [InlineData(data: "Inbox/image.jpg")]
    [InlineData(data: "Inbox/archive.zip")]
    public void MediaFamilyOf_UnknownExtensions_ReturnUnknown(string path)
    {
        InboxClassifier.MediaFamilyOf(path: path).Should().Be(expected: "unknown");
    }

    // -----------------------------------------------------------------------
    // StructuralType — video structure parse
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(data: ["Inbox/Breaking Bad/Season 01/Breaking Bad S01E01.mkv", "tv"])]
    [InlineData(data: ["Inbox/The Office 1x01.mkv", "tv"])]
    [InlineData(data: ["Inbox/Show Name S02E03 720p.mkv", "tv"])]
    [InlineData(data: ["Inbox/Game of Thrones/Season 1/S01E01.mkv", "tv"])]
    [InlineData(data: ["Inbox/Series/Season 2/Episode.mkv", "tv"])]
    public void StructuralType_TvShapes_ReturnTv(string path, string expected)
    {
        InboxClassifier.StructuralType(path: path).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["Inbox/The Matrix (1999)/The Matrix (1999).mkv", "movie"])]
    [InlineData(data: ["Inbox/Inception (2010).mkv", "movie"])]
    [InlineData(data: ["Inbox/Interstellar (2014).mkv", "movie"])]
    [InlineData(data: ["Inbox/The Dark Knight (2008)/The Dark Knight (2008).mkv", "movie"])]
    public void StructuralType_MovieShapes_ReturnMovie(string path, string expected)
    {
        InboxClassifier.StructuralType(path: path).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["Inbox/[SubsPlease] Frieren - 01 (1080p).mkv", "anime"])]
    [InlineData(data: ["Inbox/[Erai-raws] Bleach - 366 [1080p].mkv", "anime"])]
    [InlineData(data: ["Inbox/[HorribleSubs] Attack on Titan - 25 [720p].mkv", "anime"])]
    public void StructuralType_AnimeShapes_ReturnAnime(string path, string expected)
    {
        InboxClassifier.StructuralType(path: path).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: "Inbox/random-clip.mkv")]
    [InlineData(data: "Inbox/movie.mkv")]
    [InlineData(data: "Inbox/untitled.mp4")]
    public void StructuralType_AmbiguousShapes_ReturnUnknown(string path)
    {
        InboxClassifier.StructuralType(path: path).Should().Be(expected: "unknown");
    }

    // -----------------------------------------------------------------------
    // Classify — video: movie/high when single strong movie hit, no TV hit
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Classify_StrongMovieHit_NoTvHit_ReturnsMovieHigh()
    {
        Mock<IInboxMetadataProbe> probe = new();
        probe
            .Setup(expression: p => p.SearchMoviesAsync("The Matrix", 1999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: [StrongMovie(title: "The Matrix", year: 1999)]);
        probe
            .Setup(expression: p =>
                p.SearchTvAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: []);

        InboxClassifier classifier = MakeClassifier(probe: probe.Object);
        ClassificationResult result = await classifier.Classify(
            path: "Inbox/The Matrix (1999)/The Matrix (1999).mkv",
            driverId: Ulid.NewUlid(),
            ct: CancellationToken.None
        );

        result.DetectedType.Should().Be(expected: "movie");
        result.Confidence.Should().Be(expected: "high");
        result.Candidates.Should().NotBeEmpty();
        result.Candidates[0].Provider.Should().Be(expected: "tmdb");
    }

    // -----------------------------------------------------------------------
    // Classify — video: low when strong hits on BOTH movie and TV
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Classify_StrongHitsOnBothMovieAndTv_ReturnsLow()
    {
        Mock<IInboxMetadataProbe> probe = new();
        probe
            .Setup(expression: p =>
                p.SearchMoviesAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: [StrongMovie(title: "Inception", year: 2010)]);
        probe
            .Setup(expression: p =>
                p.SearchTvAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: [StrongTv(title: "Inception", year: 2010)]);

        InboxClassifier classifier = MakeClassifier(probe: probe.Object);
        ClassificationResult result = await classifier.Classify(
            path: "Inbox/Inception (2010).mkv",
            driverId: Ulid.NewUlid(),
            ct: CancellationToken.None
        );

        result.Confidence.Should().Be(expected: "low");
    }

    // -----------------------------------------------------------------------
    // Classify — video: anime structural type stays low when ambiguous
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Classify_AnimeStructureNoProviderHit_ReturnsAnimeLow()
    {
        Mock<IInboxMetadataProbe> probe = EmptyProbe();

        InboxClassifier classifier = MakeClassifier(probe: probe.Object);
        ClassificationResult result = await classifier.Classify(
            path: "Inbox/[SubsPlease] Frieren - 01 (1080p).mkv",
            driverId: Ulid.NewUlid(),
            ct: CancellationToken.None
        );

        result.DetectedType.Should().Be(expected: "anime");
        result.Confidence.Should().Be(expected: "low");
    }

    // -----------------------------------------------------------------------
    // Classify — music: tags with MusicBrainz release id → music/high
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Classify_MusicTagsWithReleaseId_ReturnsMusicHigh()
    {
        Guid releaseId = Guid.NewGuid();
        CandidateMatch candidate = MusicCandidate(releaseId: releaseId, title: "Artist – Album");

        Mock<IInboxAudioTagReader> tagReader = new();
        tagReader
            .Setup(expression: r =>
                r.ReadAsync(It.IsAny<string>(), It.IsAny<Ulid>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                value: new InboxAudioTags
                {
                    MusicBrainzReleaseId = releaseId,
                    Album = "Album",
                    Artist = "Artist",
                }
            );

        Mock<IInboxMetadataProbe> probe = new();
        probe
            .Setup(expression: p => p.LookupMusicReleaseAsync(releaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: candidate);

        InboxClassifier classifier = MakeClassifier(
            probe: probe.Object,
            tagReader: tagReader.Object
        );
        ClassificationResult result = await classifier.Classify(
            path: "Inbox/Artist - Album/01 - Track.flac",
            driverId: Ulid.NewUlid(),
            ct: CancellationToken.None
        );

        result.DetectedType.Should().Be(expected: "music");
        result.Confidence.Should().Be(expected: "high");
        result.Candidates.Should().HaveCount(expected: 1);
        result.Candidates[0].Provider.Should().Be(expected: "musicbrainz");
    }

    // -----------------------------------------------------------------------
    // Classify — music: tags present but no release id → music/medium
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Classify_MusicTagsWithoutReleaseId_ReturnsMusicMedium()
    {
        Mock<IInboxAudioTagReader> tagReader = new();
        tagReader
            .Setup(expression: r =>
                r.ReadAsync(It.IsAny<string>(), It.IsAny<Ulid>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                value: new InboxAudioTags
                {
                    MusicBrainzReleaseId = null,
                    Album = "Some Album",
                    Artist = "Some Artist",
                }
            );

        InboxClassifier classifier = MakeClassifier(tagReader: tagReader.Object);
        ClassificationResult result = await classifier.Classify(
            path: "Inbox/song.mp3",
            driverId: Ulid.NewUlid(),
            ct: CancellationToken.None
        );

        result.DetectedType.Should().Be(expected: "music");
        result.Confidence.Should().Be(expected: "medium");
    }

    // -----------------------------------------------------------------------
    // Classify — music: no tags → music/low
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Classify_MusicNoTags_ReturnsMusicLow()
    {
        Mock<IInboxAudioTagReader> tagReader = new();
        tagReader
            .Setup(expression: r =>
                r.ReadAsync(It.IsAny<string>(), It.IsAny<Ulid>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: (InboxAudioTags?)null);

        InboxClassifier classifier = MakeClassifier(tagReader: tagReader.Object);
        ClassificationResult result = await classifier.Classify(
            path: "Inbox/unknown-track.flac",
            driverId: Ulid.NewUlid(),
            ct: CancellationToken.None
        );

        result.DetectedType.Should().Be(expected: "music");
        result.Confidence.Should().Be(expected: "low");
        result.Candidates.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Task 2.6 — Confidence tuning table
    // Real-world messy naming shapes: each must land at its intended type+confidence.
    // Ambiguous cases must land at low. Network calls are all stubbed.
    // -----------------------------------------------------------------------

    // Clean movie names
    [Theory]
    [InlineData(data: ["Inbox/Parasite (2019)/Parasite (2019).mkv", "movie", "high"])]
    [InlineData(data: ["Inbox/Avengers Endgame (2019).mkv", "movie", "high"])]
    [InlineData(data: ["Inbox/Blade Runner 2049 (2017)/Blade Runner 2049 (2017).mkv", "movie", "high"])]
    public async Task ConfidenceTuning_CleanMovieNames_ReturnMovieHigh(
        string path,
        string expectedType,
        string expectedConfidence
    )
    {
        Mock<IInboxMetadataProbe> probe = new();
        string title = ExtractMovieTitleFromPath(path: path);
        int? year = ExtractYearFromPath(path: path);

        probe
            .Setup(expression: p =>
                p.SearchMoviesAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: [StrongMovie(title: title, year: year ?? 2019)]);
        probe
            .Setup(expression: p =>
                p.SearchTvAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: []);

        InboxClassifier classifier = MakeClassifier(probe: probe.Object);
        ClassificationResult result = await classifier.Classify(
            path: path,
            driverId: Ulid.NewUlid(),
            ct: CancellationToken.None
        );

        result.DetectedType.Should().Be(expected: expectedType);
        result.Confidence.Should().Be(expected: expectedConfidence);
    }

    // Clean TV show names
    [Theory]
    [InlineData(data: ["Inbox/Breaking Bad/Season 01/Breaking Bad S01E01.mkv", "tv", "high"])]
    [InlineData(data: ["Inbox/The Office/S02E03.mkv", "tv", "high"])]
    [InlineData(data: ["Inbox/Succession S03E01 720p.mkv", "tv", "high"])]
    public async Task ConfidenceTuning_CleanTvNames_ReturnTvHigh(
        string path,
        string expectedType,
        string expectedConfidence
    )
    {
        Mock<IInboxMetadataProbe> probe = new();
        probe
            .Setup(expression: p =>
                p.SearchTvAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: [StrongTv(title: "Breaking Bad", year: 2008)]);
        probe
            .Setup(expression: p =>
                p.SearchMoviesAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: []);

        InboxClassifier classifier = MakeClassifier(probe: probe.Object);
        ClassificationResult result = await classifier.Classify(
            path: path,
            driverId: Ulid.NewUlid(),
            ct: CancellationToken.None
        );

        result.DetectedType.Should().Be(expected: expectedType);
        result.Confidence.Should().Be(expected: expectedConfidence);
    }

    // Multi-episode folder — still TV
    [Fact]
    public async Task ConfidenceTuning_MultiEpisodeFolder_ReturnsTv()
    {
        Mock<IInboxMetadataProbe> probe = new();
        probe
            .Setup(expression: p =>
                p.SearchTvAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: [StrongTv(title: "The Wire", year: 2002)]);
        probe
            .Setup(expression: p =>
                p.SearchMoviesAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: []);

        InboxClassifier classifier = MakeClassifier(probe: probe.Object);
        ClassificationResult result = await classifier.Classify(
            path: "Inbox/The Wire/Season 1/S01E01-S01E03.mkv",
            driverId: Ulid.NewUlid(),
            ct: CancellationToken.None
        );

        result.DetectedType.Should().Be(expected: "tv");
    }

    // Year in folder only (not filename) — still classifies as movie
    [Fact]
    public async Task ConfidenceTuning_YearInFolderOnly_ClassifiesAsMovie()
    {
        Mock<IInboxMetadataProbe> probe = new();
        probe
            .Setup(expression: p =>
                p.SearchMoviesAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: [StrongMovie(title: "Fight Club", year: 1999)]);
        probe
            .Setup(expression: p =>
                p.SearchTvAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: []);

        InboxClassifier classifier = MakeClassifier(probe: probe.Object);
        ClassificationResult result = await classifier.Classify(
            path: "Inbox/Fight Club (1999)/fight.club.mkv",
            driverId: Ulid.NewUlid(),
            ct: CancellationToken.None
        );

        result.DetectedType.Should().Be(expected: "movie");
    }

    // Anime fansub variants — all land at anime (conservative: low or medium)
    [Theory]
    [InlineData(data: ["Inbox/[Erai-raws] One Piece - 1000 [1080p].mkv", "anime"])]
    [InlineData(data: ["Inbox/[HorribleSubs] Naruto Shippuden - 500 [720p].mkv", "anime"])]
    public async Task ConfidenceTuning_AnimeFansubVariants_ReturnAnime(
        string path,
        string expectedType
    )
    {
        InboxClassifier classifier = MakeClassifier(probe: EmptyProbe().Object);
        ClassificationResult result = await classifier.Classify(
            path: path,
            driverId: Ulid.NewUlid(),
            ct: CancellationToken.None
        );

        result.DetectedType.Should().Be(expected: expectedType);
    }

    // Ambiguous — bare filename, no year, no episode tokens → unknown/low
    [Theory]
    [InlineData(data: "Inbox/random-clip.mkv")]
    [InlineData(data: "Inbox/movie.mkv")]
    [InlineData(data: "Inbox/sample.mp4")]
    public async Task ConfidenceTuning_AmbiguousFiles_ReturnUnknownOrLow(string path)
    {
        InboxClassifier classifier = MakeClassifier(probe: EmptyProbe().Object);
        ClassificationResult result = await classifier.Classify(
            path: path,
            driverId: Ulid.NewUlid(),
            ct: CancellationToken.None
        );

        result.Confidence.Should().Be(expected: "low");
    }

    // Messy movie name (quality tags in filename) — still resolves to movie/high
    [Fact]
    public async Task ConfidenceTuning_MessyMovieFilename_ResolvesToMovieHigh()
    {
        Mock<IInboxMetadataProbe> probe = new();
        probe
            .Setup(expression: p =>
                p.SearchMoviesAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: [StrongMovie(title: "The Godfather", year: 1972)]);
        probe
            .Setup(expression: p =>
                p.SearchTvAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: []);

        InboxClassifier classifier = MakeClassifier(probe: probe.Object);
        ClassificationResult result = await classifier.Classify(
            path: "Inbox/The Godfather (1972)/The.Godfather.1972.1080p.BluRay.x264.mkv",
            driverId: Ulid.NewUlid(),
            ct: CancellationToken.None
        );

        result.DetectedType.Should().Be(expected: "movie");
        result.Confidence.Should().Be(expected: "high");
    }

    // Weak-hit movie: structural type clear but score below threshold → medium
    [Fact]
    public async Task ConfidenceTuning_WeakMovieHit_ReturnsMedium()
    {
        Mock<IInboxMetadataProbe> probe = new();
        probe
            .Setup(expression: p =>
                p.SearchMoviesAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: [WeakHit(title: "Some Obscure Movie")]);
        probe
            .Setup(expression: p =>
                p.SearchTvAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: []);

        InboxClassifier classifier = MakeClassifier(probe: probe.Object);
        ClassificationResult result = await classifier.Classify(
            path: "Inbox/Obscure (2005).mkv",
            driverId: Ulid.NewUlid(),
            ct: CancellationToken.None
        );

        result.DetectedType.Should().Be(expected: "movie");
        result.Confidence.Should().Be(expected: "medium");
    }

    // Year mismatch → medium not high
    [Fact]
    public async Task ConfidenceTuning_YearMismatch_ReturnsMedium()
    {
        // Query year = 2010 but hit year = 1990 — big mismatch → not strong
        Mock<IInboxMetadataProbe> probe = new();
        CandidateMatch yearMismatchHit = new()
        {
            Provider = "tmdb",
            ExternalId = "777",
            Title = "Total Recall",
            Year = 1990,
            Score = 0.80,
        };
        probe
            .Setup(expression: p =>
                p.SearchMoviesAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: [yearMismatchHit]);
        probe
            .Setup(expression: p =>
                p.SearchTvAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: []);

        InboxClassifier classifier = MakeClassifier(probe: probe.Object);
        ClassificationResult result = await classifier.Classify(
            path: "Inbox/Total Recall (2012).mkv",
            driverId: Ulid.NewUlid(),
            ct: CancellationToken.None
        );

        // Year in path is 2012, hit year is 1990 — mismatch > 1 year → not strong → medium
        result.DetectedType.Should().Be(expected: "movie");
        result.Confidence.Should().Be(expected: "medium");
    }

    // -----------------------------------------------------------------------
    // No network calls are made for pure static tests (no probe invocation)
    // -----------------------------------------------------------------------

    [Fact]
    public void StructuralType_NeverCallsProbe()
    {
        // This test verifies StructuralType is pure static — just call it
        // without any probe setup and it must not throw.
        string result = InboxClassifier.StructuralType(path: "Inbox/Show S01E01.mkv");
        result.Should().Be(expected: "tv");
    }

    // -----------------------------------------------------------------------
    // Private helpers used in test setup (not production code)
    // -----------------------------------------------------------------------

    private static string ExtractMovieTitleFromPath(string path)
    {
        string filename = Path.GetFileNameWithoutExtension(path: path);
        filename = System.Text.RegularExpressions.Regex.Replace(
            input: filename,
            pattern: @"\s*\((?:19|20)\d{2}\)\s*",
            replacement: string.Empty
        );
        return filename.Trim();
    }

    private static int? ExtractYearFromPath(string path)
    {
        System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
            input: path,
            pattern: @"\((?:19|20)(\d{2})\)"
        );
        if (!match.Success)
            return null;
        return int.TryParse(s: match.Value.Trim(trimChars: ['(', ')']), result: out int year) ? year : null;
    }
}
