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
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.MediaProcessing.Files;
using NoMercy.NmSystem.Domain;
using NoMercy.Storage;

namespace NoMercy.Tests.MediaProcessing.Files;

// ---------------------------------------------------------------------------
// VideoEncodeJob's post-encode registration retries FindFiles on a bounded
// backoff exactly when it comes back with 0 registered candidates — see
// VideoEncodeJob.ScanEncodedOutputWithRetryAsync. That decision hinges entirely
// on FindFiles' bool return value, so this pins the contract: an empty scan
// (here, a library whose target movie/show cannot be resolved so no folder is
// ever probed) must return false, not throw and not silently report success.
// ---------------------------------------------------------------------------
[Trait("Category", "Unit")]
public sealed class FileManagerFindFilesTests
{
    [Fact]
    public async Task FindFiles_NoResolvableMediaFolder_ReturnsFalse()
    {
        Mock<IFileRepository> repoMock = new();
        repoMock
            .Setup(repository => repository.MediaType(It.IsAny<int>(), It.IsAny<Library>()))
            .ReturnsAsync(((Movie?)null, (Tv?)null, MediaTypes.MovieMediaType));

        Mock<IStorageFactory> factoryMock = new();
        Mock<IStorageDriver> driverMock = new();

        FileManager manager = new(repoMock.Object, factoryMock.Object, driverMock.Object);

        Library library = new() { Id = Ulid.NewUlid(), Type = MediaTypes.MovieMediaType };

        // No Movie is resolvable for this id, so FileManager.Paths() has no
        // folder to look under and the scan never finds a candidate file.
        bool hasCandidates = await manager.FindFiles(id: 999_999, library);

        hasCandidates.Should().BeFalse();
    }
}
