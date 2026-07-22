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
using MovieFileLibrary;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Files.Parsing;
using NoMercy.MediaProcessing.Files.Parsing.Adapters;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Storage;

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
        storage.SetupGet(expression: s => s.Driver).Returns(value: driver.Object);

        List<StorageEntry> entries =
        [
            new(Path: "Movies/Inception.2010.1080p.BluRay.mkv", IsDirectory: false, SizeBytes: 1000, LastModified: DateTimeOffset.UtcNow),
            new(Path: "Movies/The.Matrix.1999.1080p.BluRay.mkv", IsDirectory: false, SizeBytes: 2000, LastModified: DateTimeOffset.UtcNow),
        ];
        storage
            .Setup(expression: s => s.List(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<bool>()))
            .Returns(value: entries);

        Mock<IMediaIdentificationService> identification = new();
        identification
            .Setup(expression: i =>
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
            .ReturnsAsync(value: ((MovieOrEpisode match, string? imdbId)?)null);

        FileListService service = new(
            storageDriver: driver.Object,
            identification: identification.Object,
            filenameParser: new FilenameParserPipeline(
                adapters: new IFilenameParseAdapter[]
                {
                    new EpisodePrefixAdapter(),
                    new EpisodeWordAdapter(),
                    new SeasonEpisodeAdapter(),
                    new MovieDetectorAdapter(),
                }
            ),
            logger: NullLogger<FileListService>.Instance
        );

        List<FileItem> files = await service.GetFilesInDirectory(directoryPath: "Movies", libraryType: "movie", storage: storage.Object);

        // The fix: no file is dropped just because TMDB could not identify it.
        Assert.Equal(expected: 2, actual: files.Count);
        Assert.All(collection: files, action: file => Assert.False(condition: string.IsNullOrEmpty(value: file.Parsed?.Title)));
        // Unidentified => empty Match.
        Assert.All(collection: files, action: file => Assert.True(condition: string.IsNullOrEmpty(value: file.Match.Title)));
    }
}
