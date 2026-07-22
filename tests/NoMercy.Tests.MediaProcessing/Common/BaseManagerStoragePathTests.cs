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

using FluentAssertions;
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
            FolderRootPath(storage: storage, path: path);
    }

    private static LocalStorage BuildLocalStorage()
    {
        Mock<IStorageDriver> driver = new(behavior: MockBehavior.Loose);
        driver
            .Setup(expression: b => b.GetFullPath(It.IsAny<string>()))
            .Returns<string>(valueFunction: p => Path.GetFullPath(path: p));
        driver.Setup(expression: b => b.ResolveLinkTarget(It.IsAny<string>())).Returns(value: (string?)null);

        StoragePathGuard guard = new(allowedRoots: [], driver: driver.Object);
        return new(driver: driver.Object, guard: guard);
    }

    [Fact]
    public void FolderRootPath_WithLocalStorage_ReturnsResolvedAbsolutePath()
    {
        LocalStorage storage = BuildLocalStorage();
        string scopeRelativePath = "Movies/Action";

        ConcreteBaseManager manager = new();
        string result = manager.TestFolderRootPath(storage: storage, path: scopeRelativePath);

        result.Should().NotBeNull().And.NotBeEmpty();
        result.Should().Contain(expected: "Movies");
        result.Should().Contain(expected: "Action");
    }

    [Fact]
    public void FolderRootPath_WithRemoteStorage_ReturnsScopeRelativePathUnchanged()
    {
        Mock<IStorageDriver> driver = new();
        RemoteStorage storage = new(driver: driver.Object);
        string scopeRelativePath = "Movies/Action";

        ConcreteBaseManager manager = new();
        string result = manager.TestFolderRootPath(storage: storage, path: scopeRelativePath);

        result.Should().Be(expected: scopeRelativePath);
    }

    [Fact]
    public void FolderRootPath_WithRemoteStorage_DoesNotThrowNotSupportedExceptionForGetFullPath()
    {
        Mock<IStorageDriver> driver = new();
        RemoteStorage storage = new(driver: driver.Object);
        string scopeRelativePath = "Libraries/Anime/Folder";

        ConcreteBaseManager manager = new();

        Func<string> act = () => manager.TestFolderRootPath(storage: storage, path: scopeRelativePath);

        act.Should().NotThrow();
    }

    [Fact]
    public void FolderRootPath_LocalStorageBuildsFullPath_RemoteStorageReturnsAsIs()
    {
        LocalStorage localStorage = BuildLocalStorage();
        Mock<IStorageDriver> remoteDriver = new();
        RemoteStorage remoteStorage = new(driver: remoteDriver.Object);

        string folderPath = "Anime/Monogatari";

        ConcreteBaseManager manager = new();
        string localResult = manager.TestFolderRootPath(storage: localStorage, path: folderPath);
        string remoteResult = manager.TestFolderRootPath(storage: remoteStorage, path: folderPath);

        localResult.Should().NotBe(unexpected: folderPath);
        remoteResult.Should().Be(expected: folderPath);
    }

    [Fact]
    public void FolderRootPath_WithRemoteStorageAndComplexPath_ReturnsPathUnchanged()
    {
        Mock<IStorageDriver> driver = new();
        RemoteStorage storage = new(driver: driver.Object);
        string complexPath = "Music/Artists/Pink Floyd/Albums/The Wall/Disc 1";

        ConcreteBaseManager manager = new();
        string result = manager.TestFolderRootPath(storage: storage, path: complexPath);

        result.Should().Be(expected: complexPath);
    }

    [Fact]
    public void FolderRootPath_RemoteStorageConsistentAcrossMultipleCalls()
    {
        Mock<IStorageDriver> remoteDriver = new();
        RemoteStorage remoteStorage = new(driver: remoteDriver.Object);
        string testPath = "Libraries/TestLibrary";

        ConcreteBaseManager manager = new();
        string result1 = manager.TestFolderRootPath(storage: remoteStorage, path: testPath);
        string result2 = manager.TestFolderRootPath(storage: remoteStorage, path: testPath);
        string result3 = manager.TestFolderRootPath(storage: remoteStorage, path: testPath);

        result1.Should().Be(expected: result2);
        result2.Should().Be(expected: result3);
        result1.Should().Be(expected: testPath);
    }

    [Fact]
    public void FolderRootPath_MixedLibraryScenario_LocalAndRemoteFoldersHandledCorrectly()
    {
        LocalStorage localStorage = BuildLocalStorage();
        Mock<IStorageDriver> remoteDriver = new();
        RemoteStorage remoteStorage = new(driver: remoteDriver.Object);

        ConcreteBaseManager manager = new();

        string localFolder = "LocalMovies";
        string remoteFolder = "NfsMovies";

        string localResult = manager.TestFolderRootPath(storage: localStorage, path: localFolder);
        string remoteResult = manager.TestFolderRootPath(storage: remoteStorage, path: remoteFolder);

        localResult.Should().Contain(expected: localFolder);
        remoteResult.Should().Be(expected: remoteFolder);
    }
}
