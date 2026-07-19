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
using NoMercy.MediaProcessing.Libraries;
using NoMercy.Storage;

namespace NoMercy.Tests.MediaProcessing.Libraries;

/// <summary>
/// Pins the library-scan root resolution. A configured local library stores its
/// folder path RELATIVE to the driver's rootPath (e.g. "Libraries/Anime" under
/// "\\nas\Media"). The scan must join those through the storage facade, which
/// owns the root — resolving through the low-level driver instead
/// (<c>storage.Driver.GetFullPath</c>) canonicalized the relative path against
/// the process working directory and produced a path that does not exist, so
/// every configured local library scanned "0 subfolders" and discovered nothing.
/// Remote backends don't implement the facade escape-hatch (they throw), so for
/// those the driver — which embeds its own export/prefix — is the correct source.
/// </summary>
public class LibraryManagerResolveScanRootTests
{
    [Fact]
    public void Local_folder_joins_its_rootpath_through_the_facade()
    {
        Mock<IStorage> storage = new();
        // LocalStorage.GetFullPath joins the configured rootPath with the relative
        // folder path — the real absolute scan target.
        storage
            .Setup(s => s.GetFullPath("Libraries/Anime"))
            .Returns(@"\\nas\Media\Libraries\Anime");

        string root = LibraryManager.ResolveScanRoot(storage.Object, "Libraries/Anime");

        root.Should().Be(@"\\nas\Media\Libraries\Anime");
        // The driver must never be consulted for a backend the facade can resolve:
        // its root-less GetFullPath would resolve against the process CWD.
        storage.Verify(s => s.Driver, Times.Never);
    }

    [Fact]
    public void Remote_folder_falls_back_to_the_driver_which_owns_its_export()
    {
        Mock<IStorageDriver> driver = new();
        driver
            .Setup(d => d.GetFullPath("Libraries/Anime"))
            .Returns("/mnt/vault/Media/Libraries/Anime");

        Mock<IStorage> storage = new();
        // Remote facades don't support GetFullPath — they throw. The scan must then
        // fall back to the driver, whose GetFullPath prepends the export/prefix.
        storage.Setup(s => s.GetFullPath(It.IsAny<string>())).Throws<NotSupportedException>();
        storage.Setup(s => s.Driver).Returns(driver.Object);

        string root = LibraryManager.ResolveScanRoot(storage.Object, "Libraries/Anime");

        root.Should().Be("/mnt/vault/Media/Libraries/Anime");
    }
}
