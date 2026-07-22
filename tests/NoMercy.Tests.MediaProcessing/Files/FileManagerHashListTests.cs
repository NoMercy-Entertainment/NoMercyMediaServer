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

using System.Reflection;
using Moq;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Analysis;
using NoMercy.MediaProcessing.Files;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.MediaProcessing.Files;

// ---------------------------------------------------------------------------
// Regression tests for GetVideoHashList / GetAudioHashList path handling.
//
// Bug: when LocalStorage.List() returns scope-relative paths (Enforced=true,
// i.e. after the MakeDriverIdRequiredAndSeedLocalDrivers migration) the old
// code did Path.GetRelativePath(absoluteHostFolder, scopeRelativePath) which
// produced "../../../…" garbage.  The fix replaces that with a composition of
// storage.GetName(dir.Path) + "/" + storage.GetName(playlistPath).
//
// Both modes must produce exactly "/" + variantDir + "/" + playlistFile.
// ---------------------------------------------------------------------------
[Trait(name: "Category", value: "Unit")]
public sealed class FileManagerHashListTests : IDisposable
{
    // -----------------------------------------------------------------------
    // Private-method helpers via reflection
    // -----------------------------------------------------------------------

    private static FileManager BuildFileManager()
    {
        Mock<IFileRepository> repoMock = new();
        Mock<IStorageFactory> factoryMock = new();
        Mock<IStorageDriver> driverMock = new();
        Mock<IMediaAnalyzer> mediaAnalyzerMock = new();
        return new(
            fileRepository: repoMock.Object,
            storageFactory: factoryMock.Object,
            storageDriver: driverMock.Object,
            mediaAnalyzer: mediaAnalyzerMock.Object
        );
    }

    private static List<IVideo> InvokeGetVideoHashList(
        FileManager manager,
        IStorage storage,
        string hostFolder
    )
    {
        MethodInfo method =
            typeof(FileManager).GetMethod(
                name: "GetVideoHashList",
                bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
            ) ?? throw new InvalidOperationException(message: "GetVideoHashList not found");

        return (List<IVideo>)method.Invoke(obj: manager, parameters: [storage, hostFolder])!;
    }

    private static List<IAudio> InvokeGetAudioHashList(
        FileManager manager,
        IStorage storage,
        string hostFolder
    )
    {
        MethodInfo method =
            typeof(FileManager).GetMethod(
                name: "GetAudioHashList",
                bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
            ) ?? throw new InvalidOperationException(message: "GetAudioHashList not found");

        return (List<IAudio>)method.Invoke(obj: manager, parameters: [storage, hostFolder])!;
    }

    // -----------------------------------------------------------------------
    // Temp directory scaffolding
    // -----------------------------------------------------------------------

    private readonly string _tempRoot;

    public FileManagerHashListTests()
    {
        _tempRoot = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _tempRoot))
            Directory.Delete(path: _tempRoot, recursive: true);
    }

    /// <summary>
    /// Builds the on-disk layout under <paramref name="hostDir"/>:
    ///   hostDir/video_1920x1080_SDR/video_1920x1080_SDR.m3u8
    ///   hostDir/audio_eng_aac/audio_eng_aac.m3u8
    /// </summary>
    private static void CreateScannerLayout(string hostDir)
    {
        string videoDir = Path.Combine(path1: hostDir, path2: "video_1920x1080_SDR");
        Directory.CreateDirectory(path: videoDir);
        File.WriteAllText(path: Path.Combine(path1: videoDir, path2: "video_1920x1080_SDR.m3u8"), contents: "#EXTM3U");
        File.WriteAllText(path: Path.Combine(path1: videoDir, path2: "segment0.ts"), contents: "data");

        string audioDir = Path.Combine(path1: hostDir, path2: "audio_eng_aac");
        Directory.CreateDirectory(path: audioDir);
        File.WriteAllText(path: Path.Combine(path1: audioDir, path2: "audio_eng_aac.m3u8"), contents: "#EXTM3U");
    }

    // -----------------------------------------------------------------------
    // Enforced=false  (fresh install — guard has no root, List returns abs paths)
    // -----------------------------------------------------------------------

    [Fact]
    public void GetVideoHashList_EnforcedFalse_ReturnsCorrectHostRelativeFileName()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.(2020).NoMercy");
        CreateScannerLayout(hostDir: hostDir);

        LocalStorageDriver driver = new();
        StoragePathGuard guard = new(allowedRoots: [], driver: driver);
        LocalStorage storage = new(driver: driver, guard: guard);

        FileManager manager = BuildFileManager();

        List<IVideo> results = InvokeGetVideoHashList(manager: manager, storage: storage, hostFolder: hostDir);

        results.Should().HaveCount(expected: 1);
        results[index: 0].FileName.Should().Be(expected: "/video_1920x1080_SDR/video_1920x1080_SDR.m3u8");
        results[index: 0].FileName.Should().NotContain(unexpected: "..");
    }

    [Fact]
    public void GetAudioHashList_EnforcedFalse_ReturnsCorrectHostRelativeFileName()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.(2020).NoMercy");
        CreateScannerLayout(hostDir: hostDir);

        LocalStorageDriver driver = new();
        StoragePathGuard guard = new(allowedRoots: [], driver: driver);
        LocalStorage storage = new(driver: driver, guard: guard);

        FileManager manager = BuildFileManager();

        List<IAudio> results = InvokeGetAudioHashList(manager: manager, storage: storage, hostFolder: hostDir);

        results.Should().HaveCount(expected: 1);
        results[index: 0].FileName.Should().Be(expected: "/audio_eng_aac/audio_eng_aac.m3u8");
        results[index: 0].FileName.Should().NotContain(unexpected: "..");
    }

    // -----------------------------------------------------------------------
    // Enforced=true  (post-migration — guard has root, List returns scope-relative paths)
    // -----------------------------------------------------------------------

    [Fact]
    public void GetVideoHashList_EnforcedTrue_ReturnsCorrectHostRelativeFileName()
    {
        string root = _tempRoot;
        string hostDir = Path.Combine(path1: root, path2: "Movie.(2020).NoMercy");
        CreateScannerLayout(hostDir: hostDir);

        LocalStorageDriver driver = new();
        StoragePathGuard guard = new(allowedRoots: [root], driver: driver);
        LocalStorage storage = new(driver: driver, guard: guard);

        // hostFolder is still the absolute OS path — this is how callers supply it.
        // storage.List() will return scope-relative paths under Enforced=true.
        FileManager manager = BuildFileManager();

        List<IVideo> results = InvokeGetVideoHashList(manager: manager, storage: storage, hostFolder: hostDir);

        results.Should().HaveCount(expected: 1);
        results[index: 0].FileName.Should().Be(expected: "/video_1920x1080_SDR/video_1920x1080_SDR.m3u8");
        results[index: 0].FileName.Should().NotContain(unexpected: "..");
    }

    [Fact]
    public void GetAudioHashList_EnforcedTrue_ReturnsCorrectHostRelativeFileName()
    {
        string root = _tempRoot;
        string hostDir = Path.Combine(path1: root, path2: "Movie.(2020).NoMercy");
        CreateScannerLayout(hostDir: hostDir);

        LocalStorageDriver driver = new();
        StoragePathGuard guard = new(allowedRoots: [root], driver: driver);
        LocalStorage storage = new(driver: driver, guard: guard);

        FileManager manager = BuildFileManager();

        List<IAudio> results = InvokeGetAudioHashList(manager: manager, storage: storage, hostFolder: hostDir);

        results.Should().HaveCount(expected: 1);
        results[index: 0].FileName.Should().Be(expected: "/audio_eng_aac/audio_eng_aac.m3u8");
        results[index: 0].FileName.Should().NotContain(unexpected: "..");
    }

    // -----------------------------------------------------------------------
    // Both modes produce the SAME value — no behavioral divergence
    // -----------------------------------------------------------------------

    [Fact]
    public void GetVideoHashList_BothModes_ProduceSameFileName()
    {
        string root = _tempRoot;
        string hostDir = Path.Combine(path1: root, path2: "Movie.(2020).NoMercy");
        CreateScannerLayout(hostDir: hostDir);

        LocalStorageDriver driver = new();

        LocalStorage openStorage = new(driver: driver, guard: new(allowedRoots: [], driver: driver));
        LocalStorage enforcedStorage = new(driver: driver, guard: new(allowedRoots: [root], driver: driver));

        FileManager manager = BuildFileManager();

        List<IVideo> openResults = InvokeGetVideoHashList(manager: manager, storage: openStorage, hostFolder: hostDir);
        List<IVideo> enforcedResults = InvokeGetVideoHashList(manager: manager, storage: enforcedStorage, hostFolder: hostDir);

        openResults.Should().HaveCount(expected: 1);
        enforcedResults.Should().HaveCount(expected: 1);
        openResults[index: 0].FileName.Should().Be(expected: enforcedResults[index: 0].FileName);
    }

    [Fact]
    public void GetAudioHashList_BothModes_ProduceSameFileName()
    {
        string root = _tempRoot;
        string hostDir = Path.Combine(path1: root, path2: "Movie.(2020).NoMercy");
        CreateScannerLayout(hostDir: hostDir);

        LocalStorageDriver driver = new();

        LocalStorage openStorage = new(driver: driver, guard: new(allowedRoots: [], driver: driver));
        LocalStorage enforcedStorage = new(driver: driver, guard: new(allowedRoots: [root], driver: driver));

        FileManager manager = BuildFileManager();

        List<IAudio> openResults = InvokeGetAudioHashList(manager: manager, storage: openStorage, hostFolder: hostDir);
        List<IAudio> enforcedResults = InvokeGetAudioHashList(manager: manager, storage: enforcedStorage, hostFolder: hostDir);

        openResults.Should().HaveCount(expected: 1);
        enforcedResults.Should().HaveCount(expected: 1);
        openResults[index: 0].FileName.Should().Be(expected: enforcedResults[index: 0].FileName);
    }
}
