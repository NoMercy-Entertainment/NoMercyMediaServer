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
using NoMercy.MediaProcessing.Common;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Remote;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.MediaProcessing.Common;

public class BaseManagerStoragePathTests
{
    private sealed class ConcreteBaseManager : BaseManager
    {
        public string TestFolderRootPath(IStorage storage, string path) =>
            FolderRootPath(storage, path);
    }

    private static LocalStorage BuildLocalStorage()
    {
        Mock<IStorageDriver> driver = new(MockBehavior.Loose);
        driver
            .Setup(b => b.GetFullPath(It.IsAny<string>()))
            .Returns<string>(p => Path.GetFullPath(p));
        driver.Setup(b => b.ResolveLinkTarget(It.IsAny<string>())).Returns((string?)null);

        StoragePathGuard guard = new([], driver.Object);
        return new(driver.Object, guard);
    }

    [Fact]
    public void FolderRootPath_WithLocalStorage_ReturnsResolvedAbsolutePath()
    {
        LocalStorage storage = BuildLocalStorage();
        string scopeRelativePath = "Movies/Action";

        ConcreteBaseManager manager = new();
        string result = manager.TestFolderRootPath(storage, scopeRelativePath);

        result.Should().NotBeNull().And.NotBeEmpty();
        result.Should().Contain("Movies");
        result.Should().Contain("Action");
    }

    [Fact]
    public void FolderRootPath_WithRemoteStorage_ReturnsScopeRelativePathUnchanged()
    {
        Mock<IStorageDriver> driver = new();
        RemoteStorage storage = new(driver.Object);
        string scopeRelativePath = "Movies/Action";

        ConcreteBaseManager manager = new();
        string result = manager.TestFolderRootPath(storage, scopeRelativePath);

        result.Should().Be(scopeRelativePath);
    }

    [Fact]
    public void FolderRootPath_WithRemoteStorage_DoesNotThrowNotSupportedExceptionForGetFullPath()
    {
        Mock<IStorageDriver> driver = new();
        RemoteStorage storage = new(driver.Object);
        string scopeRelativePath = "Libraries/Anime/Folder";

        ConcreteBaseManager manager = new();

        Func<string> act = () => manager.TestFolderRootPath(storage, scopeRelativePath);

        act.Should().NotThrow();
    }

    [Fact]
    public void FolderRootPath_LocalStorageBuildsFullPath_RemoteStorageReturnsAsIs()
    {
        LocalStorage localStorage = BuildLocalStorage();
        Mock<IStorageDriver> remoteDriver = new();
        RemoteStorage remoteStorage = new(remoteDriver.Object);

        string folderPath = "Anime/Monogatari";

        ConcreteBaseManager manager = new();
        string localResult = manager.TestFolderRootPath(localStorage, folderPath);
        string remoteResult = manager.TestFolderRootPath(remoteStorage, folderPath);

        localResult.Should().NotBe(folderPath);
        remoteResult.Should().Be(folderPath);
    }

    [Fact]
    public void FolderRootPath_WithRemoteStorageAndComplexPath_ReturnsPathUnchanged()
    {
        Mock<IStorageDriver> driver = new();
        RemoteStorage storage = new(driver.Object);
        string complexPath = "Music/Artists/Pink Floyd/Albums/The Wall/Disc 1";

        ConcreteBaseManager manager = new();
        string result = manager.TestFolderRootPath(storage, complexPath);

        result.Should().Be(complexPath);
    }

    [Fact]
    public void FolderRootPath_RemoteStorageConsistentAcrossMultipleCalls()
    {
        Mock<IStorageDriver> remoteDriver = new();
        RemoteStorage remoteStorage = new(remoteDriver.Object);
        string testPath = "Libraries/TestLibrary";

        ConcreteBaseManager manager = new();
        string result1 = manager.TestFolderRootPath(remoteStorage, testPath);
        string result2 = manager.TestFolderRootPath(remoteStorage, testPath);
        string result3 = manager.TestFolderRootPath(remoteStorage, testPath);

        result1.Should().Be(result2);
        result2.Should().Be(result3);
        result1.Should().Be(testPath);
    }

    [Fact]
    public void FolderRootPath_MixedLibraryScenario_LocalAndRemoteFoldersHandledCorrectly()
    {
        LocalStorage localStorage = BuildLocalStorage();
        Mock<IStorageDriver> remoteDriver = new();
        RemoteStorage remoteStorage = new(remoteDriver.Object);

        ConcreteBaseManager manager = new();

        string localFolder = "LocalMovies";
        string remoteFolder = "NfsMovies";

        string localResult = manager.TestFolderRootPath(localStorage, localFolder);
        string remoteResult = manager.TestFolderRootPath(remoteStorage, remoteFolder);

        localResult.Should().Contain(localFolder);
        remoteResult.Should().Be(remoteFolder);
    }
}
