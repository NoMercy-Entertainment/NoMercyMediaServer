using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Subtitles;
using NoMercy.Storage;

namespace NoMercy.Tests.Encoder.Subtitles;

/// <summary>
/// SubtitleAcquisitionService orchestrates the strategy chain (Hash → Filename
/// → Title), filtering (rating / downloads / trusted / fps), and selection
/// (top per language). Tests pin the contract that the encode is never
/// blocked by a subtitle acquisition fault — every adapter exception lands
/// as an empty result, not a propagating throw.
/// </summary>
public class SubtitleAcquisitionServiceTests
{
    private static SubtitleAcquisitionService BuildService(
        out Mock<IOpenSubtitlesAdapter> adapter,
        out Mock<IStorage> storage
    )
    {
        adapter = new();
        storage = new();
        return new(adapter.Object, storage.Object, NullLogger<SubtitleAcquisitionService>.Instance);
    }

    private static AcquisitionRequest MakeRequest(
        SubtitleAcquisitionConfig? config = null,
        string[]? languagesAlreadyInSource = null,
        double? sourceFps = null
    ) =>
        new(
            SourcePath: "/media/movie.mkv",
            SourceFileSize: 1_000_000_000,
            SourceFilename: "movie.mkv",
            MediaTitle: "The Movie",
            Season: null,
            Episode: null,
            Year: 2024,
            SourceFps: sourceFps,
            SourceDuration: TimeSpan.FromHours(2),
            LanguagesAlreadyInSource: languagesAlreadyInSource ?? [],
            Config: config ?? new SubtitleAcquisitionConfig { Enabled = true, Languages = ["eng"] }
        );

    private static SubtitleCandidate Candidate(
        string language = "eng",
        double rating = 9.0,
        int downloads = 1000,
        bool trusted = false,
        double? fps = null
    ) =>
        new(
            Provider: "opensubtitles",
            Language: language,
            Rating: rating,
            Downloads: downloads,
            IsTrustedUploader: trusted,
            Fps: fps,
            DownloadUrl: "https://example.invalid/sub.srt.gz",
            Format: "srt"
        );

    [Fact]
    public async Task AcquireAsync_Disabled_ReturnsEmpty()
    {
        SubtitleAcquisitionService subject = BuildService(
            out Mock<IOpenSubtitlesAdapter> adapter,
            out _
        );
        AcquisitionRequest request = MakeRequest(new SubtitleAcquisitionConfig { Enabled = false });

        IReadOnlyList<AcquiredSubtitle> result = await subject.AcquireAsync(
            request,
            CancellationToken.None
        );

        result.Should().BeEmpty();
        adapter.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AcquireAsync_NoLanguages_ReturnsEmpty()
    {
        SubtitleAcquisitionService subject = BuildService(
            out Mock<IOpenSubtitlesAdapter> adapter,
            out _
        );
        AcquisitionRequest request = MakeRequest(
            new SubtitleAcquisitionConfig { Enabled = true, Languages = [] }
        );

        IReadOnlyList<AcquiredSubtitle> result = await subject.AcquireAsync(
            request,
            CancellationToken.None
        );

        result.Should().BeEmpty();
        adapter.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AcquireAsync_FillMissingOnly_RemovesLanguagesAlreadyPresent()
    {
        // Config asks for eng + fra; source already has eng — only fra survives.
        SubtitleAcquisitionService subject = BuildService(
            out Mock<IOpenSubtitlesAdapter> adapter,
            out Mock<IStorage> storage
        );
        StubStorage(storage);

        adapter
            .Setup(a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.Is<string[]>(langs => langs.SequenceEqual(new[] { "fra" })),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ReturnsAsync([Candidate(language: "fra")]);
        adapter
            .Setup(a =>
                a.DownloadAsync(It.IsAny<SubtitleCandidate>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([1, 2, 3]);

        AcquisitionRequest request = MakeRequest(
            config: new SubtitleAcquisitionConfig
            {
                Enabled = true,
                Languages = ["eng", "fra"],
                FillMissingOnly = true,
                Strategy = SubtitleMatchStrategy.HashOnly,
            },
            languagesAlreadyInSource: ["eng"]
        );

        IReadOnlyList<AcquiredSubtitle> result = await subject.AcquireAsync(
            request,
            CancellationToken.None
        );

        result.Should().ContainSingle();
        result[0].Language.Should().Be("fra");
        adapter.Verify(
            a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.Is<string[]>(langs => langs.SequenceEqual(new[] { "fra" })),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task AcquireAsync_RateLimited_ReturnsEmpty()
    {
        // Rate limiting is a known + expected condition. The service catches
        // the typed exception and returns empty so the encode keeps going.
        SubtitleAcquisitionService subject = BuildService(
            out Mock<IOpenSubtitlesAdapter> adapter,
            out _
        );
        adapter
            .Setup(a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ThrowsAsync(new OpenSubtitlesRateLimitException("429 — too many calls"));

        AcquisitionRequest request = MakeRequest(
            new SubtitleAcquisitionConfig
            {
                Enabled = true,
                Languages = ["eng"],
                Strategy = SubtitleMatchStrategy.HashOnly,
            }
        );

        IReadOnlyList<AcquiredSubtitle> result = await subject.AcquireAsync(
            request,
            CancellationToken.None
        );

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AcquireAsync_AdapterThrowsGenericException_ReturnsEmpty()
    {
        // Any other exception (network, JSON error, …) also lands as empty.
        SubtitleAcquisitionService subject = BuildService(
            out Mock<IOpenSubtitlesAdapter> adapter,
            out _
        );
        adapter
            .Setup(a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ThrowsAsync(new HttpRequestException("DNS fail"));

        AcquisitionRequest request = MakeRequest(
            new SubtitleAcquisitionConfig
            {
                Enabled = true,
                Languages = ["eng"],
                Strategy = SubtitleMatchStrategy.HashOnly,
            }
        );

        Func<Task<IReadOnlyList<AcquiredSubtitle>>> act = () =>
            subject.AcquireAsync(request, CancellationToken.None);

        IReadOnlyList<AcquiredSubtitle> result = await act();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AcquireAsync_CancellationToken_ReturnsEmpty()
    {
        SubtitleAcquisitionService subject = BuildService(
            out Mock<IOpenSubtitlesAdapter> adapter,
            out _
        );
        adapter
            .Setup(a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ThrowsAsync(new OperationCanceledException());

        AcquisitionRequest request = MakeRequest(
            new SubtitleAcquisitionConfig
            {
                Enabled = true,
                Languages = ["eng"],
                Strategy = SubtitleMatchStrategy.HashOnly,
            }
        );

        IReadOnlyList<AcquiredSubtitle> result = await subject.AcquireAsync(
            request,
            CancellationToken.None
        );

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AcquireAsync_HashHits_DoesNotFallThroughToFilename()
    {
        // Strategy = HashThenFilename. Hash returns results → filename search
        // MUST NOT run (extra API call would waste a precious daily quota).
        SubtitleAcquisitionService subject = BuildService(
            out Mock<IOpenSubtitlesAdapter> adapter,
            out Mock<IStorage> storage
        );
        StubStorage(storage);

        adapter
            .Setup(a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ReturnsAsync([Candidate()]);
        adapter
            .Setup(a =>
                a.DownloadAsync(It.IsAny<SubtitleCandidate>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([1, 2, 3]);

        AcquisitionRequest request = MakeRequest(
            new SubtitleAcquisitionConfig
            {
                Enabled = true,
                Languages = ["eng"],
                Strategy = SubtitleMatchStrategy.HashThenFilename,
            }
        );

        _ = await subject.AcquireAsync(request, CancellationToken.None);

        adapter.Verify(
            a =>
                a.SearchByFilenameAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                ),
            Times.Never,
            "filename search must not fire when hash already matched"
        );
    }

    [Fact]
    public async Task AcquireAsync_HashMisses_FallsThroughToFilename()
    {
        SubtitleAcquisitionService subject = BuildService(
            out Mock<IOpenSubtitlesAdapter> adapter,
            out Mock<IStorage> storage
        );
        StubStorage(storage);

        adapter
            .Setup(a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ReturnsAsync([]);
        adapter
            .Setup(a =>
                a.SearchByFilenameAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ReturnsAsync([Candidate()]);
        adapter
            .Setup(a =>
                a.DownloadAsync(It.IsAny<SubtitleCandidate>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([1, 2, 3]);

        AcquisitionRequest request = MakeRequest(
            new SubtitleAcquisitionConfig
            {
                Enabled = true,
                Languages = ["eng"],
                Strategy = SubtitleMatchStrategy.HashThenFilename,
            }
        );

        IReadOnlyList<AcquiredSubtitle> result = await subject.AcquireAsync(
            request,
            CancellationToken.None
        );

        result.Should().ContainSingle();
        adapter.Verify(
            a =>
                a.SearchByFilenameAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task AcquireAsync_TitleOnlyStrategy_SkipsHashAndFilename()
    {
        SubtitleAcquisitionService subject = BuildService(
            out Mock<IOpenSubtitlesAdapter> adapter,
            out Mock<IStorage> storage
        );
        StubStorage(storage);

        adapter
            .Setup(a =>
                a.SearchByTitleAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ReturnsAsync([Candidate()]);
        adapter
            .Setup(a =>
                a.DownloadAsync(It.IsAny<SubtitleCandidate>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([1, 2, 3]);

        AcquisitionRequest request = MakeRequest(
            new SubtitleAcquisitionConfig
            {
                Enabled = true,
                Languages = ["eng"],
                Strategy = SubtitleMatchStrategy.TitleOnly,
            }
        );

        _ = await subject.AcquireAsync(request, CancellationToken.None);

        adapter.Verify(
            a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                ),
            Times.Never
        );
        adapter.Verify(
            a =>
                a.SearchByFilenameAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task AcquireAsync_FiltersOutBelowMinRating()
    {
        SubtitleAcquisitionService subject = BuildService(
            out Mock<IOpenSubtitlesAdapter> adapter,
            out Mock<IStorage> storage
        );
        StubStorage(storage);

        adapter
            .Setup(a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ReturnsAsync([
                Candidate(rating: 9.5),
                Candidate(rating: 3.0), // below the floor — must be filtered
            ]);
        adapter
            .Setup(a =>
                a.DownloadAsync(It.IsAny<SubtitleCandidate>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([1, 2, 3]);

        AcquisitionRequest request = MakeRequest(
            new SubtitleAcquisitionConfig
            {
                Enabled = true,
                Languages = ["eng"],
                MinRating = 5.0,
                MaxPerLanguage = 5,
                Strategy = SubtitleMatchStrategy.HashOnly,
            }
        );

        IReadOnlyList<AcquiredSubtitle> result = await subject.AcquireAsync(
            request,
            CancellationToken.None
        );

        result.Should().ContainSingle();
        result[0].Rating.Should().BeGreaterThan(5.0);
    }

    [Fact]
    public async Task AcquireAsync_FpsMismatch_FiltersWhenRequireMatchingFps()
    {
        SubtitleAcquisitionService subject = BuildService(
            out Mock<IOpenSubtitlesAdapter> adapter,
            out Mock<IStorage> storage
        );
        StubStorage(storage);

        adapter
            .Setup(a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ReturnsAsync([
                Candidate(fps: 23.976), // matches source 24.0 within tolerance
                Candidate(fps: 29.97), // mismatch — must be filtered
            ]);
        adapter
            .Setup(a =>
                a.DownloadAsync(It.IsAny<SubtitleCandidate>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([1, 2, 3]);

        AcquisitionRequest request = MakeRequest(
            config: new SubtitleAcquisitionConfig
            {
                Enabled = true,
                Languages = ["eng"],
                MaxPerLanguage = 5,
                RequireMatchingFps = true,
                Strategy = SubtitleMatchStrategy.HashOnly,
            },
            sourceFps: 24.0
        );

        IReadOnlyList<AcquiredSubtitle> result = await subject.AcquireAsync(
            request,
            CancellationToken.None
        );

        result.Should().ContainSingle("only the 23.976 candidate passes the fps gate");
    }

    [Fact]
    public async Task AcquireAsync_TrustedUploadersOnly_DropsUntrusted()
    {
        SubtitleAcquisitionService subject = BuildService(
            out Mock<IOpenSubtitlesAdapter> adapter,
            out Mock<IStorage> storage
        );
        StubStorage(storage);

        adapter
            .Setup(a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ReturnsAsync([Candidate(trusted: true), Candidate(trusted: false)]);
        adapter
            .Setup(a =>
                a.DownloadAsync(It.IsAny<SubtitleCandidate>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([1, 2, 3]);

        AcquisitionRequest request = MakeRequest(
            new SubtitleAcquisitionConfig
            {
                Enabled = true,
                Languages = ["eng"],
                TrustedUploadersOnly = true,
                MaxPerLanguage = 5,
                Strategy = SubtitleMatchStrategy.HashOnly,
            }
        );

        IReadOnlyList<AcquiredSubtitle> result = await subject.AcquireAsync(
            request,
            CancellationToken.None
        );

        result.Should().ContainSingle();
    }

    [Fact]
    public async Task AcquireAsync_MaxPerLanguage_TakesTopRanked()
    {
        // Sort key = Rating * log10(downloads + 1) — must take the highest
        // composite score per language up to MaxPerLanguage.
        SubtitleAcquisitionService subject = BuildService(
            out Mock<IOpenSubtitlesAdapter> adapter,
            out Mock<IStorage> storage
        );
        StubStorage(storage);

        adapter
            .Setup(a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ReturnsAsync([
                Candidate(rating: 9.0, downloads: 10),
                Candidate(rating: 9.5, downloads: 100_000),
                Candidate(rating: 8.0, downloads: 50),
            ]);
        adapter
            .Setup(a =>
                a.DownloadAsync(It.IsAny<SubtitleCandidate>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([1, 2, 3]);

        AcquisitionRequest request = MakeRequest(
            new SubtitleAcquisitionConfig
            {
                Enabled = true,
                Languages = ["eng"],
                MaxPerLanguage = 1,
                Strategy = SubtitleMatchStrategy.HashOnly,
            }
        );

        IReadOnlyList<AcquiredSubtitle> result = await subject.AcquireAsync(
            request,
            CancellationToken.None
        );

        result.Should().ContainSingle();
        // 9.5 × log10(100001) ≈ 47.5 — the dominant score.
        result[0].Rating.Should().Be(9.5);
        result[0].Downloads.Should().Be(100_000);
    }

    [Fact]
    public async Task AcquireAsync_DownloadThrows_SkipsCandidate()
    {
        // Download failure for one candidate must NOT abort the whole batch.
        SubtitleAcquisitionService subject = BuildService(
            out Mock<IOpenSubtitlesAdapter> adapter,
            out Mock<IStorage> storage
        );
        StubStorage(storage);

        SubtitleCandidate good = Candidate(language: "eng");
        SubtitleCandidate bad = Candidate(language: "fra");

        adapter
            .Setup(a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ReturnsAsync([good, bad]);
        adapter
            .Setup(a => a.DownloadAsync(good, It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2, 3]);
        adapter
            .Setup(a => a.DownloadAsync(bad, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("ECONNRESET"));

        AcquisitionRequest request = MakeRequest(
            new SubtitleAcquisitionConfig
            {
                Enabled = true,
                Languages = ["eng", "fra"],
                MaxPerLanguage = 1,
                Strategy = SubtitleMatchStrategy.HashOnly,
            }
        );

        IReadOnlyList<AcquiredSubtitle> result = await subject.AcquireAsync(
            request,
            CancellationToken.None
        );

        result.Should().ContainSingle();
        result[0].Language.Should().Be("eng");
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static void StubStorage(Mock<IStorage> storage)
    {
        storage
            .Setup(s => s.CombinePath(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((a, b) => $"{a}/{b}");
        storage.Setup(s => s.CreateDirectory(It.IsAny<string>()));
        storage
            .Setup(s =>
                s.WriteAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>())
            )
            .Returns(Task.CompletedTask);
        storage.Setup(s => s.GetFullPath(It.IsAny<string>())).Returns<string>(p => $"/storage/{p}");
    }
}
