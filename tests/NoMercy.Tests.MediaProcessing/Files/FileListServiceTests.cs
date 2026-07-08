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
using MovieFileLibrary;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Files.Parsing;
using NoMercy.MediaProcessing.Files.Parsing.Adapters;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Storage;
using Microsoft.Extensions.Logging.Abstractions;
namespace NoMercy.Tests.MediaProcessing.Files;

/// <summary>
/// Portable (no NAS, no TMDB) gate for slice 16.6: a video file whose
/// identification yields null must STILL be returned as a FileItem with its
/// parsed title — identification is enrichment, never a filter.
/// </summary>
public class FileListServiceTests
{
    [Fact]
    public async Task GetFilesInDirectory_EmitsFileItemPerVideo_WhenIdentificationReturnsNull()
    {
        Mock<IStorageDriver> driver = new();
        Mock<IStorage> storage = new();
        storage.SetupGet(s => s.Driver).Returns(driver.Object);

        List<StorageEntry> entries =
        [
            new(
                "Movies/Inception.2010.1080p.BluRay.mkv",
                false,
                1000,
                DateTimeOffset.UtcNow
            ),
            new(
                "Movies/The.Matrix.1999.1080p.BluRay.mkv",
                false,
                2000,
                DateTimeOffset.UtcNow
            ),
        ];
        storage
            .Setup(s => s.List(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<bool>()))
            .Returns(entries);

        Mock<IMediaIdentificationService> identification = new();
        identification
            .Setup(i =>
                i.IdentifyAsync(
                    It.IsAny<MovieFile>(),
                    It.IsAny<string>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<int?>(),
                    It.IsAny<bool>(),
                    It.IsAny<DateOnly?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(((MovieOrEpisode match, string? imdbId)?)null);

        FileListService service = new(driver.Object, identification.Object, new FilenameParserPipeline(new IFilenameParseAdapter[] { new EpisodePrefixAdapter(), new EpisodeWordAdapter(), new SeasonEpisodeAdapter(), new MovieDetectorAdapter() }), NullLogger<FileListService>.Instance);

        List<FileItem> files = await service.GetFilesInDirectory("Movies", "movie", storage.Object);

        // The fix: no file is dropped just because TMDB could not identify it.
        Assert.Equal(2, files.Count);
        Assert.All(files, file => Assert.False(string.IsNullOrEmpty(file.Parsed?.Title)));
        // Unidentified => empty Match.
        Assert.All(files, file => Assert.True(string.IsNullOrEmpty(file.Match.Title)));
    }
}
