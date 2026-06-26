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
using NoMercy.MediaProcessing.Files;
using NoMercy.Storage;

namespace NoMercy.Tests.MediaProcessing.Files;

/// <summary>
/// Verifies that <c>FileManager.MoveFolderAsync</c> uses a stream-copy path when
/// source and destination storages are on different backend types, and the atomic
/// <c>MoveDirectory</c> fast-path when they share the same backend type.
/// </summary>
[Trait("Category", "Unit")]
public class MoveFolderCrossBackendTests
{
    private sealed class DriverA : IStorageDriver
    {
        public bool FileExists(string path) => false;

        public bool DirectoryExists(string path) => false;

        public void CreateDirectory(string path) { }

        public void DeleteFile(string path) { }

        public void DeleteDirectory(string path, bool recursive) { }

        public long GetFileSize(string path) => 0;

        public DateTime GetLastWriteTimeUtc(string path) => DateTime.UtcNow;

        public DateTime GetCreationTimeUtc(string path) => DateTime.UtcNow;

        public DateTime GetLastAccessTimeUtc(string path) => DateTime.UtcNow;

        public Stream OpenRead(string path) => Stream.Null;

        public Stream OpenWrite(string path, bool overwrite) => Stream.Null;

        public void MoveFile(string source, string destination) { }

        public void CopyFile(string source, string destination, bool overwrite) { }

        public void MoveDirectory(string source, string destination) { }

        public IEnumerable<string> EnumerateFileSystemEntries(
            string directory,
            string searchPattern,
            SearchOption option
        ) => [];

        public string GetFullPath(string path) => path;

        public string? ResolveLinkTarget(string path) => null;

        public bool IsHidden(string path) => false;
    }

    private sealed class DriverB : IStorageDriver
    {
        public bool FileExists(string path) => false;

        public bool DirectoryExists(string path) => false;

        public void CreateDirectory(string path) { }

        public void DeleteFile(string path) { }

        public void DeleteDirectory(string path, bool recursive) { }

        public long GetFileSize(string path) => 0;

        public DateTime GetLastWriteTimeUtc(string path) => DateTime.UtcNow;

        public DateTime GetCreationTimeUtc(string path) => DateTime.UtcNow;

        public DateTime GetLastAccessTimeUtc(string path) => DateTime.UtcNow;

        public Stream OpenRead(string path) => Stream.Null;

        public Stream OpenWrite(string path, bool overwrite) => Stream.Null;

        public void MoveFile(string source, string destination) { }

        public void CopyFile(string source, string destination, bool overwrite) { }

        public void MoveDirectory(string source, string destination) { }

        public IEnumerable<string> EnumerateFileSystemEntries(
            string directory,
            string searchPattern,
            SearchOption option
        ) => [];

        public string GetFullPath(string path) => path;

        public string? ResolveLinkTarget(string path) => null;

        public bool IsHidden(string path) => false;
    }

    [Fact]
    public async Task SameBackendType_UsesMoveDirectory()
    {
        DriverA driver = new();

        int moveDirectoryCalls = 0;

        Mock<IStorage> source = new();
        source.Setup(s => s.Driver).Returns(driver);
        source.Setup(s => s.Exists("source/folder")).Returns(true);
        source
            .Setup(s => s.MoveDirectory("source/folder", "dest/folder"))
            .Callback(() => moveDirectoryCalls++);

        Mock<IStorage> dest = new();
        dest.Setup(s => s.Driver).Returns(driver);

        MoveFolderAccessor accessor = new(source.Object, dest.Object);
        await accessor.InvokeMoveFolderAsync("source/folder", "dest/folder");

        moveDirectoryCalls.Should().Be(1, "same-backend uses atomic MoveDirectory");
        source.Verify(s => s.OpenRead(It.IsAny<string>()), Times.Never);
        dest.Verify(s => s.OpenWrite(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task DifferentBackendTypes_StreamCopiesFilesAndDeletesSource()
    {
        DriverA localDriver = new();
        DriverB remoteDriver = new();

        byte[] fileContent = [1, 2, 3, 4, 5];

        StorageEntry fileEntry = new(
            Path: "source/folder/track.flac",
            IsDirectory: false,
            SizeBytes: fileContent.Length,
            LastModified: DateTimeOffset.UtcNow
        );

        Mock<IStorage> source = new();
        source.Setup(s => s.Driver).Returns(localDriver);
        source.Setup(s => s.Exists("source/folder")).Returns(true);
        source.Setup(s => s.List("source/folder", null, true)).Returns([fileEntry]);
        source
            .Setup(s => s.OpenRead("source/folder/track.flac"))
            .Returns(new MemoryStream(fileContent));
        bool deleteDirectoryCalled = false;
        source
            .Setup(s => s.DeleteDirectory("source/folder", true))
            .Callback(() => deleteDirectoryCalled = true);

        MemoryStream capturedWrite = new();
        Mock<IStorage> dest = new();
        dest.Setup(s => s.Driver).Returns(remoteDriver);
        dest.Setup(s => s.GetParent(It.IsAny<string>())).Returns("dest/folder");
        dest.Setup(s => s.CreateDirectory(It.IsAny<string>()));
        dest.Setup(s => s.OpenWrite(It.IsAny<string>(), true)).Returns(capturedWrite);

        MoveFolderAccessor accessor = new(source.Object, dest.Object);
        await accessor.InvokeMoveFolderAsync("source/folder", "dest/folder");

        capturedWrite
            .ToArray()
            .Should()
            .BeEquivalentTo(fileContent, "file bytes must be streamed across backends");
        deleteDirectoryCalled
            .Should()
            .BeTrue("source directory is removed after cross-backend copy completes");
        source.Verify(s => s.MoveDirectory(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SourceNotFound_ThrowsDirectoryNotFoundException()
    {
        DriverA driver = new();

        Mock<IStorage> source = new();
        source.Setup(s => s.Driver).Returns(driver);
        source.Setup(s => s.Exists(It.IsAny<string>())).Returns(false);

        Mock<IStorage> dest = new();
        dest.Setup(s => s.Driver).Returns(driver);

        MoveFolderAccessor accessor = new(source.Object, dest.Object);
        Func<Task> act = () => accessor.InvokeMoveFolderAsync("missing/folder", "dest/folder");

        await act.Should().ThrowAsync<DirectoryNotFoundException>();
    }

    [Fact]
    public async Task DifferentBackendTypes_DirectoryEntriesAreSkipped()
    {
        DriverA localDriver = new();
        DriverB remoteDriver = new();

        StorageEntry dirEntry = new(
            Path: "source/folder/sub",
            IsDirectory: true,
            SizeBytes: 0,
            LastModified: DateTimeOffset.UtcNow
        );

        Mock<IStorage> source = new();
        source.Setup(s => s.Driver).Returns(localDriver);
        source.Setup(s => s.Exists("source/folder")).Returns(true);
        source.Setup(s => s.List("source/folder", null, true)).Returns([dirEntry]);
        source.Setup(s => s.DeleteDirectory("source/folder", true));

        Mock<IStorage> dest = new();
        dest.Setup(s => s.Driver).Returns(remoteDriver);

        MoveFolderAccessor accessor = new(source.Object, dest.Object);
        await accessor.InvokeMoveFolderAsync("source/folder", "dest/folder");

        dest.Verify(s => s.OpenWrite(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task DifferentBackendTypes_MultipleFiles_AllCopied()
    {
        DriverA localDriver = new();
        DriverB remoteDriver = new();

        StorageEntry[] entries =
        [
            new("source/folder/track01.flac", false, 100, DateTimeOffset.UtcNow),
            new("source/folder/track02.flac", false, 200, DateTimeOffset.UtcNow),
            new("source/folder/cover.jpg", false, 50, DateTimeOffset.UtcNow),
        ];

        Mock<IStorage> source = new();
        source.Setup(s => s.Driver).Returns(localDriver);
        source.Setup(s => s.Exists("source/folder")).Returns(true);
        source.Setup(s => s.List("source/folder", null, true)).Returns(entries);
        source.Setup(s => s.OpenRead(It.IsAny<string>())).Returns(() => new MemoryStream([0]));
        source.Setup(s => s.DeleteDirectory("source/folder", true));

        int writeCount = 0;
        Mock<IStorage> dest = new();
        dest.Setup(s => s.Driver).Returns(remoteDriver);
        dest.Setup(s => s.GetParent(It.IsAny<string>())).Returns("dest/folder");
        dest.Setup(s => s.CreateDirectory(It.IsAny<string>()));
        dest.Setup(s => s.OpenWrite(It.IsAny<string>(), true))
            .Returns(() =>
            {
                writeCount++;
                return new MemoryStream();
            });

        MoveFolderAccessor accessor = new(source.Object, dest.Object);
        await accessor.InvokeMoveFolderAsync("source/folder", "dest/folder");

        writeCount.Should().Be(3, "each of the three files is written to the destination");
    }

    private sealed class MoveFolderAccessor(IStorage sourceStorage, IStorage destStorage)
    {
        public Task InvokeMoveFolderAsync(string sourceFolder, string destFolder)
        {
            System.Reflection.MethodInfo? method = typeof(FileManager).GetMethod(
                "MoveFolderAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
            );

            if (method is null)
                throw new MissingMethodException(nameof(FileManager), "MoveFolderAsync");

            object? result = method.Invoke(
                null,
                [sourceFolder, destFolder, sourceStorage, destStorage]
            );

            return (Task)result!;
        }
    }
}
