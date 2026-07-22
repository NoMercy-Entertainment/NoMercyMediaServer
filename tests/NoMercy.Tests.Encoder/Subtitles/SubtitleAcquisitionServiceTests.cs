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
public class SubtitleAcquisitionServiceTests : IDisposable
{
    // The hash strategy only runs when the source is readable, so a source that exists is part of
    // the fixture rather than a detail: point these at a missing path and every test below
    // silently exercises the filename fallback instead of the chain it names.
    private readonly string _sourcePath = Path.Combine(
        path1: Path.GetTempPath(),
        path2: $"nomercy-subs-{Guid.NewGuid():N}.mkv"
    );

    public SubtitleAcquisitionServiceTests()
    {
        byte[] content = new byte[256 * 1024];
        Random.Shared.NextBytes(buffer: content);
        File.WriteAllBytes(path: _sourcePath, bytes: content);
    }

    public void Dispose()
    {
        if (File.Exists(path: _sourcePath))
            File.Delete(path: _sourcePath);
        GC.SuppressFinalize(obj: this);
    }

    private static SubtitleAcquisitionService BuildService(
        out Mock<IOpenSubtitlesAdapter> adapter,
        out Mock<IStorage> storage
    )
    {
        adapter = new();
        storage = new();
        return new(adapter: adapter.Object, storage: storage.Object, logger: NullLogger<SubtitleAcquisitionService>.Instance);
    }

    private AcquisitionRequest MakeRequest(
        SubtitleAcquisitionConfig? config = null,
        string[]? languagesAlreadyInSource = null,
        double? sourceFps = null
    ) =>
        new(
            SourcePath: _sourcePath,
            SourceFileSize: new FileInfo(fileName: _sourcePath).Length,
            SourceFilename: "movie.mkv",
            MediaTitle: "The Movie",
            Season: null,
            Episode: null,
            Year: 2024,
            SourceFps: sourceFps,
            SourceDuration: TimeSpan.FromHours(hours: 2),
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
            adapter: out Mock<IOpenSubtitlesAdapter> adapter,
            storage: out _
        );
        AcquisitionRequest request = MakeRequest(config: new() { Enabled = false });

        IReadOnlyList<AcquiredSubtitle> result = await subject.AcquireAsync(
            request: request,
            ct: CancellationToken.None
        );

        result.Should().BeEmpty();
        adapter.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AcquireAsync_NoLanguages_ReturnsEmpty()
    {
        SubtitleAcquisitionService subject = BuildService(
            adapter: out Mock<IOpenSubtitlesAdapter> adapter,
            storage: out _
        );
        AcquisitionRequest request = MakeRequest(config: new() { Enabled = true, Languages = [] });

        IReadOnlyList<AcquiredSubtitle> result = await subject.AcquireAsync(
            request: request,
            ct: CancellationToken.None
        );

        result.Should().BeEmpty();
        adapter.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AcquireAsync_FillMissingOnly_RemovesLanguagesAlreadyPresent()
    {
        // Config asks for eng + fra; source already has eng — only fra survives.
        SubtitleAcquisitionService subject = BuildService(
            adapter: out Mock<IOpenSubtitlesAdapter> adapter,
            storage: out Mock<IStorage> storage
        );
        StubStorage(storage: storage);

        adapter
            .Setup(expression: a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.Is<string[]>(langs => langs.SequenceEqual(new[] { "fra" })),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ReturnsAsync(value: [Candidate(language: "fra")]);
        adapter
            .Setup(expression: a =>
                a.DownloadAsync(It.IsAny<SubtitleCandidate>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: [1, 2, 3]);

        AcquisitionRequest request = MakeRequest(
            config: new()
            {
                Enabled = true,
                Languages = ["eng", "fra"],
                FillMissingOnly = true,
                Strategy = SubtitleMatchStrategy.HashOnly,
            },
            languagesAlreadyInSource: ["eng"]
        );

        IReadOnlyList<AcquiredSubtitle> result = await subject.AcquireAsync(
            request: request,
            ct: CancellationToken.None
        );

        result.Should().ContainSingle();
        result[index: 0].Language.Should().Be(expected: "fra");
        adapter.Verify(
            expression: a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.Is<string[]>(langs => langs.SequenceEqual(new[] { "fra" })),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task AcquireAsync_RateLimited_ReturnsEmpty()
    {
        // Rate limiting is a known + expected condition. The service catches
        // the typed exception and returns empty so the encode keeps going.
        SubtitleAcquisitionService subject = BuildService(
            adapter: out Mock<IOpenSubtitlesAdapter> adapter,
            storage: out _
        );
        adapter
            .Setup(expression: a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ThrowsAsync(exception: new OpenSubtitlesRateLimitException(message: "429 — too many calls"));

        AcquisitionRequest request = MakeRequest(
            config: new()
            {
                Enabled = true,
                Languages = ["eng"],
                Strategy = SubtitleMatchStrategy.HashOnly,
            }
        );

        IReadOnlyList<AcquiredSubtitle> result = await subject.AcquireAsync(
            request: request,
            ct: CancellationToken.None
        );

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AcquireAsync_AdapterThrowsGenericException_ReturnsEmpty()
    {
        // Any other exception (network, JSON error, …) also lands as empty.
        SubtitleAcquisitionService subject = BuildService(
            adapter: out Mock<IOpenSubtitlesAdapter> adapter,
            storage: out _
        );
        adapter
            .Setup(expression: a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ThrowsAsync(exception: new HttpRequestException(message: "DNS fail"));

        AcquisitionRequest request = MakeRequest(
            config: new()
            {
                Enabled = true,
                Languages = ["eng"],
                Strategy = SubtitleMatchStrategy.HashOnly,
            }
        );

        Func<Task<IReadOnlyList<AcquiredSubtitle>>> act = () =>
            subject.AcquireAsync(request: request, ct: CancellationToken.None);

        IReadOnlyList<AcquiredSubtitle> result = await act();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AcquireAsync_CancellationToken_ReturnsEmpty()
    {
        SubtitleAcquisitionService subject = BuildService(
            adapter: out Mock<IOpenSubtitlesAdapter> adapter,
            storage: out _
        );
        adapter
            .Setup(expression: a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ThrowsAsync(exception: new OperationCanceledException());

        AcquisitionRequest request = MakeRequest(
            config: new()
            {
                Enabled = true,
                Languages = ["eng"],
                Strategy = SubtitleMatchStrategy.HashOnly,
            }
        );

        IReadOnlyList<AcquiredSubtitle> result = await subject.AcquireAsync(
            request: request,
            ct: CancellationToken.None
        );

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AcquireAsync_HashHits_DoesNotFallThroughToFilename()
    {
        // Strategy = HashThenFilename. Hash returns results → filename search
        // MUST NOT run (extra API call would waste a precious daily quota).
        SubtitleAcquisitionService subject = BuildService(
            adapter: out Mock<IOpenSubtitlesAdapter> adapter,
            storage: out Mock<IStorage> storage
        );
        StubStorage(storage: storage);

        adapter
            .Setup(expression: a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ReturnsAsync(value: [Candidate()]);
        adapter
            .Setup(expression: a =>
                a.DownloadAsync(It.IsAny<SubtitleCandidate>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: [1, 2, 3]);

        AcquisitionRequest request = MakeRequest(
            config: new()
            {
                Enabled = true,
                Languages = ["eng"],
                Strategy = SubtitleMatchStrategy.HashThenFilename,
            }
        );

        _ = await subject.AcquireAsync(request: request, ct: CancellationToken.None);

        adapter.Verify(
            expression: a =>
                a.SearchByFilenameAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                ),
            times: Times.Never,
            failMessage: "filename search must not fire when hash already matched"
        );
    }

    [Fact]
    public async Task AcquireAsync_UnreadableSource_SkipsTheHashSearchEntirely()
    {
        // A source the service cannot open yields no moviehash, so the hash search must not run:
        // hashing the file size instead produces a value that cannot match, spending a request per
        // item to guarantee a miss and still falling through.
        SubtitleAcquisitionService subject = BuildService(
            adapter: out Mock<IOpenSubtitlesAdapter> adapter,
            storage: out Mock<IStorage> storage
        );
        StubStorage(storage: storage);

        adapter
            .Setup(expression: a =>
                a.SearchByFilenameAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ReturnsAsync(value: [Candidate()]);
        adapter
            .Setup(expression: a =>
                a.DownloadAsync(It.IsAny<SubtitleCandidate>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: [1, 2, 3]);

        AcquisitionRequest request = MakeRequest(
            config: new()
            {
                Enabled = true,
                Languages = ["eng"],
                Strategy = SubtitleMatchStrategy.HashThenFilename,
            }
        ) with
        {
            SourcePath = Path.Combine(
                path1: Path.GetTempPath(),
                path2: $"nomercy-missing-{Guid.NewGuid():N}.mkv"
            ),
        };

        _ = await subject.AcquireAsync(request: request, ct: CancellationToken.None);

        adapter.Verify(
            expression: a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                ),
            times: Times.Never,
            failMessage: "hash search must not fire when the source yields no moviehash"
        );
    }

    [Fact]
    public async Task AcquireAsync_UnreadableSource_StillAcquiresViaFilename()
    {
        SubtitleAcquisitionService subject = BuildService(
            adapter: out Mock<IOpenSubtitlesAdapter> adapter,
            storage: out Mock<IStorage> storage
        );
        StubStorage(storage: storage);

        adapter
            .Setup(expression: a =>
                a.SearchByFilenameAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ReturnsAsync(value: [Candidate()]);
        adapter
            .Setup(expression: a =>
                a.DownloadAsync(It.IsAny<SubtitleCandidate>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: [1, 2, 3]);

        AcquisitionRequest request = MakeRequest(
            config: new()
            {
                Enabled = true,
                Languages = ["eng"],
                Strategy = SubtitleMatchStrategy.HashThenFilename,
            }
        ) with
        {
            SourcePath = Path.Combine(
                path1: Path.GetTempPath(),
                path2: $"nomercy-missing-{Guid.NewGuid():N}.mkv"
            ),
        };

        IReadOnlyList<AcquiredSubtitle> result = await subject.AcquireAsync(
            request: request,
            ct: CancellationToken.None
        );

        Assert.Single(collection: result);
        // Never a hash match, so it can never be reported as an exact one.
        Assert.False(condition: result[index: 0].IsExactMatch);
    }

    [Fact]
    public async Task AcquireAsync_HashMisses_FallsThroughToFilename()
    {
        SubtitleAcquisitionService subject = BuildService(
            adapter: out Mock<IOpenSubtitlesAdapter> adapter,
            storage: out Mock<IStorage> storage
        );
        StubStorage(storage: storage);

        adapter
            .Setup(expression: a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ReturnsAsync(value: []);
        adapter
            .Setup(expression: a =>
                a.SearchByFilenameAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ReturnsAsync(value: [Candidate()]);
        adapter
            .Setup(expression: a =>
                a.DownloadAsync(It.IsAny<SubtitleCandidate>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: [1, 2, 3]);

        AcquisitionRequest request = MakeRequest(
            config: new()
            {
                Enabled = true,
                Languages = ["eng"],
                Strategy = SubtitleMatchStrategy.HashThenFilename,
            }
        );

        IReadOnlyList<AcquiredSubtitle> result = await subject.AcquireAsync(
            request: request,
            ct: CancellationToken.None
        );

        result.Should().ContainSingle();
        adapter.Verify(
            expression: a =>
                a.SearchByFilenameAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task AcquireAsync_TitleOnlyStrategy_SkipsHashAndFilename()
    {
        SubtitleAcquisitionService subject = BuildService(
            adapter: out Mock<IOpenSubtitlesAdapter> adapter,
            storage: out Mock<IStorage> storage
        );
        StubStorage(storage: storage);

        adapter
            .Setup(expression: a =>
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
            .ReturnsAsync(value: [Candidate()]);
        adapter
            .Setup(expression: a =>
                a.DownloadAsync(It.IsAny<SubtitleCandidate>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: [1, 2, 3]);

        AcquisitionRequest request = MakeRequest(
            config: new()
            {
                Enabled = true,
                Languages = ["eng"],
                Strategy = SubtitleMatchStrategy.TitleOnly,
            }
        );

        _ = await subject.AcquireAsync(request: request, ct: CancellationToken.None);

        adapter.Verify(
            expression: a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                ),
            times: Times.Never
        );
        adapter.Verify(
            expression: a =>
                a.SearchByFilenameAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                ),
            times: Times.Never
        );
    }

    [Fact]
    public async Task AcquireAsync_FiltersOutBelowMinRating()
    {
        SubtitleAcquisitionService subject = BuildService(
            adapter: out Mock<IOpenSubtitlesAdapter> adapter,
            storage: out Mock<IStorage> storage
        );
        StubStorage(storage: storage);

        adapter
            .Setup(expression: a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ReturnsAsync(value:
            [
                Candidate(rating: 9.5),
                Candidate(rating: 3.0), // below the floor — must be filtered
            ]);
        adapter
            .Setup(expression: a =>
                a.DownloadAsync(It.IsAny<SubtitleCandidate>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: [1, 2, 3]);

        AcquisitionRequest request = MakeRequest(
            config: new()
            {
                Enabled = true,
                Languages = ["eng"],
                MinRating = 5.0,
                MaxPerLanguage = 5,
                Strategy = SubtitleMatchStrategy.HashOnly,
            }
        );

        IReadOnlyList<AcquiredSubtitle> result = await subject.AcquireAsync(
            request: request,
            ct: CancellationToken.None
        );

        result.Should().ContainSingle();
        result[index: 0].Rating.Should().BeGreaterThan(expected: 5.0);
    }

    [Fact]
    public async Task AcquireAsync_FpsMismatch_FiltersWhenRequireMatchingFps()
    {
        SubtitleAcquisitionService subject = BuildService(
            adapter: out Mock<IOpenSubtitlesAdapter> adapter,
            storage: out Mock<IStorage> storage
        );
        StubStorage(storage: storage);

        adapter
            .Setup(expression: a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ReturnsAsync(value:
            [
                Candidate(fps: 23.976), // matches source 24.0 within tolerance
                Candidate(fps: 29.97), // mismatch — must be filtered
            ]);
        adapter
            .Setup(expression: a =>
                a.DownloadAsync(It.IsAny<SubtitleCandidate>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: [1, 2, 3]);

        AcquisitionRequest request = MakeRequest(
            config: new()
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
            request: request,
            ct: CancellationToken.None
        );

        result.Should().ContainSingle(because: "only the 23.976 candidate passes the fps gate");
    }

    [Fact]
    public async Task AcquireAsync_TrustedUploadersOnly_DropsUntrusted()
    {
        SubtitleAcquisitionService subject = BuildService(
            adapter: out Mock<IOpenSubtitlesAdapter> adapter,
            storage: out Mock<IStorage> storage
        );
        StubStorage(storage: storage);

        adapter
            .Setup(expression: a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ReturnsAsync(value: [Candidate(trusted: true), Candidate(trusted: false)]);
        adapter
            .Setup(expression: a =>
                a.DownloadAsync(It.IsAny<SubtitleCandidate>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: [1, 2, 3]);

        AcquisitionRequest request = MakeRequest(
            config: new()
            {
                Enabled = true,
                Languages = ["eng"],
                TrustedUploadersOnly = true,
                MaxPerLanguage = 5,
                Strategy = SubtitleMatchStrategy.HashOnly,
            }
        );

        IReadOnlyList<AcquiredSubtitle> result = await subject.AcquireAsync(
            request: request,
            ct: CancellationToken.None
        );

        result.Should().ContainSingle();
    }

    [Fact]
    public async Task AcquireAsync_MaxPerLanguage_TakesTopRanked()
    {
        // Sort key = Rating * log10(downloads + 1) — must take the highest
        // composite score per language up to MaxPerLanguage.
        SubtitleAcquisitionService subject = BuildService(
            adapter: out Mock<IOpenSubtitlesAdapter> adapter,
            storage: out Mock<IStorage> storage
        );
        StubStorage(storage: storage);

        adapter
            .Setup(expression: a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ReturnsAsync(value:
            [
                Candidate(rating: 9.0, downloads: 10),
                Candidate(rating: 9.5, downloads: 100_000),
                Candidate(rating: 8.0, downloads: 50),
            ]);
        adapter
            .Setup(expression: a =>
                a.DownloadAsync(It.IsAny<SubtitleCandidate>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: [1, 2, 3]);

        AcquisitionRequest request = MakeRequest(
            config: new()
            {
                Enabled = true,
                Languages = ["eng"],
                MaxPerLanguage = 1,
                Strategy = SubtitleMatchStrategy.HashOnly,
            }
        );

        IReadOnlyList<AcquiredSubtitle> result = await subject.AcquireAsync(
            request: request,
            ct: CancellationToken.None
        );

        result.Should().ContainSingle();
        // 9.5 × log10(100001) ≈ 47.5 — the dominant score.
        result[index: 0].Rating.Should().Be(expected: 9.5);
        result[index: 0].Downloads.Should().Be(expected: 100_000);
    }

    [Fact]
    public async Task AcquireAsync_DownloadThrows_SkipsCandidate()
    {
        // Download failure for one candidate must NOT abort the whole batch.
        SubtitleAcquisitionService subject = BuildService(
            adapter: out Mock<IOpenSubtitlesAdapter> adapter,
            storage: out Mock<IStorage> storage
        );
        StubStorage(storage: storage);

        SubtitleCandidate good = Candidate(language: "eng");
        SubtitleCandidate bad = Candidate(language: "fra");

        adapter
            .Setup(expression: a =>
                a.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()
                )
            )
            .ReturnsAsync(value: [good, bad]);
        adapter
            .Setup(expression: a => a.DownloadAsync(good, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: [1, 2, 3]);
        adapter
            .Setup(expression: a => a.DownloadAsync(bad, It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception: new HttpRequestException(message: "ECONNRESET"));

        AcquisitionRequest request = MakeRequest(
            config: new()
            {
                Enabled = true,
                Languages = ["eng", "fra"],
                MaxPerLanguage = 1,
                Strategy = SubtitleMatchStrategy.HashOnly,
            }
        );

        IReadOnlyList<AcquiredSubtitle> result = await subject.AcquireAsync(
            request: request,
            ct: CancellationToken.None
        );

        result.Should().ContainSingle();
        result[index: 0].Language.Should().Be(expected: "eng");
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static void StubStorage(Mock<IStorage> storage)
    {
        storage
            .Setup(expression: s => s.CombinePath(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>(valueFunction: (a, b) => $"{a}/{b}");
        storage.Setup(expression: s => s.CreateDirectory(It.IsAny<string>()));
        storage
            .Setup(expression: s =>
                s.WriteAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>())
            )
            .Returns(value: Task.CompletedTask);
        storage.Setup(expression: s => s.GetFullPath(It.IsAny<string>())).Returns<string>(valueFunction: p => $"/storage/{p}");
    }
}
