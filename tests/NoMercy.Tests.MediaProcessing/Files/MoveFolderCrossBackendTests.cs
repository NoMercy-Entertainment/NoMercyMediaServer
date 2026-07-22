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
[Trait(name: "Category", value: "Unit")]
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
        source.Setup(expression: s => s.Driver).Returns(value: driver);
        source.Setup(expression: s => s.Exists("source/folder")).Returns(value: true);
        source
            .Setup(expression: s => s.MoveDirectory("source/folder", "dest/folder"))
            .Callback(action: () => moveDirectoryCalls++);

        Mock<IStorage> dest = new();
        dest.Setup(expression: s => s.Driver).Returns(value: driver);

        MoveFolderAccessor accessor = new(sourceStorage: source.Object, destStorage: dest.Object);
        await accessor.InvokeMoveFolderAsync(sourceFolder: "source/folder", destFolder: "dest/folder");

        moveDirectoryCalls.Should().Be(expected: 1, because: "same-backend uses atomic MoveDirectory");
        source.Verify(expression: s => s.OpenRead(It.IsAny<string>()), times: Times.Never);
        dest.Verify(expression: s => s.OpenWrite(It.IsAny<string>(), It.IsAny<bool>()), times: Times.Never);
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
        source.Setup(expression: s => s.Driver).Returns(value: localDriver);
        source.Setup(expression: s => s.Exists("source/folder")).Returns(value: true);
        source.Setup(expression: s => s.List("source/folder", null, true)).Returns(value: [fileEntry]);
        source
            .Setup(expression: s => s.OpenRead("source/folder/track.flac"))
            .Returns(value: new MemoryStream(buffer: fileContent));
        bool deleteDirectoryCalled = false;
        source
            .Setup(expression: s => s.DeleteDirectory("source/folder", true))
            .Callback(action: () => deleteDirectoryCalled = true);

        MemoryStream capturedWrite = new();
        Mock<IStorage> dest = new();
        dest.Setup(expression: s => s.Driver).Returns(value: remoteDriver);
        dest.Setup(expression: s => s.GetParent(It.IsAny<string>())).Returns(value: "dest/folder");
        dest.Setup(expression: s => s.CreateDirectory(It.IsAny<string>()));
        dest.Setup(expression: s => s.OpenWrite(It.IsAny<string>(), true)).Returns(value: capturedWrite);

        MoveFolderAccessor accessor = new(sourceStorage: source.Object, destStorage: dest.Object);
        await accessor.InvokeMoveFolderAsync(sourceFolder: "source/folder", destFolder: "dest/folder");

        capturedWrite
            .ToArray()
            .Should()
            .BeEquivalentTo(expectation: fileContent, because: "file bytes must be streamed across backends");
        deleteDirectoryCalled
            .Should()
            .BeTrue(because: "source directory is removed after cross-backend copy completes");
        source.Verify(expression: s => s.MoveDirectory(It.IsAny<string>(), It.IsAny<string>()), times: Times.Never);
    }

    [Fact]
    public async Task SourceNotFound_ThrowsDirectoryNotFoundException()
    {
        DriverA driver = new();

        Mock<IStorage> source = new();
        source.Setup(expression: s => s.Driver).Returns(value: driver);
        source.Setup(expression: s => s.Exists(It.IsAny<string>())).Returns(value: false);

        Mock<IStorage> dest = new();
        dest.Setup(expression: s => s.Driver).Returns(value: driver);

        MoveFolderAccessor accessor = new(sourceStorage: source.Object, destStorage: dest.Object);
        Func<Task> act = () => accessor.InvokeMoveFolderAsync(sourceFolder: "missing/folder", destFolder: "dest/folder");

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
        source.Setup(expression: s => s.Driver).Returns(value: localDriver);
        source.Setup(expression: s => s.Exists("source/folder")).Returns(value: true);
        source.Setup(expression: s => s.List("source/folder", null, true)).Returns(value: [dirEntry]);
        source.Setup(expression: s => s.DeleteDirectory("source/folder", true));

        Mock<IStorage> dest = new();
        dest.Setup(expression: s => s.Driver).Returns(value: remoteDriver);

        MoveFolderAccessor accessor = new(sourceStorage: source.Object, destStorage: dest.Object);
        await accessor.InvokeMoveFolderAsync(sourceFolder: "source/folder", destFolder: "dest/folder");

        dest.Verify(expression: s => s.OpenWrite(It.IsAny<string>(), It.IsAny<bool>()), times: Times.Never);
    }

    [Fact]
    public async Task DifferentBackendTypes_MultipleFiles_AllCopied()
    {
        DriverA localDriver = new();
        DriverB remoteDriver = new();

        StorageEntry[] entries =
        [
            new(Path: "source/folder/track01.flac", IsDirectory: false, SizeBytes: 100, LastModified: DateTimeOffset.UtcNow),
            new(Path: "source/folder/track02.flac", IsDirectory: false, SizeBytes: 200, LastModified: DateTimeOffset.UtcNow),
            new(Path: "source/folder/cover.jpg", IsDirectory: false, SizeBytes: 50, LastModified: DateTimeOffset.UtcNow),
        ];

        Mock<IStorage> source = new();
        source.Setup(expression: s => s.Driver).Returns(value: localDriver);
        source.Setup(expression: s => s.Exists("source/folder")).Returns(value: true);
        source.Setup(expression: s => s.List("source/folder", null, true)).Returns(value: entries);
        source.Setup(expression: s => s.OpenRead(It.IsAny<string>())).Returns(valueFunction: () => new MemoryStream(buffer: [0]));
        source.Setup(expression: s => s.DeleteDirectory("source/folder", true));

        int writeCount = 0;
        Mock<IStorage> dest = new();
        dest.Setup(expression: s => s.Driver).Returns(value: remoteDriver);
        dest.Setup(expression: s => s.GetParent(It.IsAny<string>())).Returns(value: "dest/folder");
        dest.Setup(expression: s => s.CreateDirectory(It.IsAny<string>()));
        dest.Setup(expression: s => s.OpenWrite(It.IsAny<string>(), true))
            .Returns(valueFunction: () =>
            {
                writeCount++;
                return new MemoryStream();
            });

        MoveFolderAccessor accessor = new(sourceStorage: source.Object, destStorage: dest.Object);
        await accessor.InvokeMoveFolderAsync(sourceFolder: "source/folder", destFolder: "dest/folder");

        writeCount.Should().Be(expected: 3, because: "each of the three files is written to the destination");
    }

    // -------------------------------------------------------------------
    // sameBackend's ReferenceEquals(source, destination) short-circuit —
    // the source and destination storage instance are literally the same
    // object (e.g. moving within one library's own folder). Every other
    // test above uses two distinct Mock<IStorage> instances of the same
    // driver TYPE, which only exercises the second half of the `||`.
    // -------------------------------------------------------------------
    [Fact]
    public async Task SameStorageInstanceForSourceAndDestination_UsesMoveDirectory()
    {
        DriverA driver = new();
        int moveDirectoryCalls = 0;

        Mock<IStorage> storage = new();
        storage.Setup(expression: s => s.Driver).Returns(value: driver);
        storage.Setup(expression: s => s.Exists("source/folder")).Returns(value: true);
        storage
            .Setup(expression: s => s.MoveDirectory("source/folder", "dest/folder"))
            .Callback(action: () => moveDirectoryCalls++);

        MoveFolderAccessor accessor = new(sourceStorage: storage.Object, destStorage: storage.Object);
        await accessor.InvokeMoveFolderAsync(sourceFolder: "source/folder", destFolder: "dest/folder");

        moveDirectoryCalls
            .Should()
            .Be(expected: 1, because: "ReferenceEquals(source, destination) alone must short-circuit to same-backend");
    }

    // -------------------------------------------------------------------
    // relativePath's ternary false branch: an entry whose Path does NOT
    // start with sourceFolder (a driver that returns entries in a
    // different form than the scan root it was given) falls back to the
    // raw entry.Path unchanged, rather than throwing on the substring slice.
    // -------------------------------------------------------------------
    [Fact]
    public async Task DifferentBackendTypes_EntryPathNotPrefixedBySourceFolder_UsesRawEntryPath()
    {
        DriverA localDriver = new();
        DriverB remoteDriver = new();

        byte[] fileContent = [9, 9, 9];
        // Deliberately NOT prefixed with "source/folder" — exercises the
        // ternary's else branch (relativePath = entry.Path, unmodified).
        StorageEntry fileEntry = new(
            Path: "unrelated/track.flac",
            IsDirectory: false,
            SizeBytes: fileContent.Length,
            LastModified: DateTimeOffset.UtcNow
        );

        Mock<IStorage> source = new();
        source.Setup(expression: s => s.Driver).Returns(value: localDriver);
        source.Setup(expression: s => s.Exists("source/folder")).Returns(value: true);
        source.Setup(expression: s => s.List("source/folder", null, true)).Returns(value: [fileEntry]);
        source
            .Setup(expression: s => s.OpenRead("unrelated/track.flac"))
            .Returns(value: new MemoryStream(buffer: fileContent));
        source.Setup(expression: s => s.DeleteDirectory("source/folder", true));

        string? capturedDestPath = null;
        MemoryStream capturedWrite = new();
        Mock<IStorage> dest = new();
        dest.Setup(expression: s => s.Driver).Returns(value: remoteDriver);
        dest.Setup(expression: s => s.GetParent(It.IsAny<string>())).Returns(value: "dest/folder");
        dest.Setup(expression: s => s.CreateDirectory(It.IsAny<string>()));
        dest.Setup(expression: s => s.OpenWrite(It.IsAny<string>(), true))
            .Callback<string, bool>(action: (path, _) => capturedDestPath = path)
            .Returns(value: capturedWrite);

        MoveFolderAccessor accessor = new(sourceStorage: source.Object, destStorage: dest.Object);
        await accessor.InvokeMoveFolderAsync(sourceFolder: "source/folder", destFolder: "dest/folder");

        capturedDestPath
            .Should()
            .Be(
                expected: "dest/folder/unrelated/track.flac",
                because: "the raw entry.Path is joined onto the destination unchanged when it isn't prefixed by sourceFolder"
            );
    }

    private sealed class MoveFolderAccessor(IStorage sourceStorage, IStorage destStorage)
    {
        public Task InvokeMoveFolderAsync(string sourceFolder, string destFolder)
        {
            System.Reflection.MethodInfo? method = typeof(FileManager).GetMethod(
                name: "MoveFolderAsync",
                bindingAttr: System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
            );

            if (method is null)
                throw new MissingMethodException(className: nameof(FileManager), methodName: "MoveFolderAsync");

            object? result = method.Invoke(
                obj: null,
                parameters: [sourceFolder, destFolder, sourceStorage, destStorage]
            );

            return (Task)result!;
        }
    }
}
