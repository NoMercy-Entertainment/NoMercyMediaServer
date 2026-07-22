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
using NoMercy.Providers.TMDB.Client;
using NoMercy.Storage;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
/// Guards the empty/slash-only image path case in TmdbImageClient.Download.
/// An entity with no poster/backdrop reaches Download with an empty path;
/// path.Replace("/", "") then collapses to an empty file name and the write
/// target becomes the "original" cache DIRECTORY itself, producing
/// "Access to the path '…/cache/images/original' is denied" at runtime. The
/// guard must short-circuit before any filesystem write.
/// </summary>
[Trait(name: "Category", value: "Unit")]
[Collection(name: "TmdbImageClient")]
public class TmdbImageClientDownloadGuardTests
{
    [Theory]
    [InlineData(data: "")]
    [InlineData(data: "/")]
    [InlineData(data: "   ")]
    public async Task Download_EmptyOrSlashOnlyPath_NeverWritesToStorage(string path)
    {
        Mock<IStorage> storage = new(behavior: MockBehavior.Loose);
        storage
            .Setup(expression: s => s.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: false);
        storage
            .Setup(expression: s => s.CreateDirectoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(value: Task.CompletedTask);

        TmdbImageClient.Initialize(storage: storage.Object);

        Task<SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>?>? download =
            TmdbImageClient.Download(path: path);
        if (download is not null)
            await download;

        // The whole point: an empty file name must NOT reach WriteAsync, which
        // would target the 'original' directory and throw access-denied.
        storage.Verify(
            expression: s =>
                s.WriteAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            times: Times.Never,
            failMessage: "an empty/slash-only image path must short-circuit before any write to the images cache"
        );
    }
}
