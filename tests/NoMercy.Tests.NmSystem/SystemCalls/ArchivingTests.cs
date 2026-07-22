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

using System.Formats.Tar;
using System.IO.Compression;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.NmSystem.SystemCalls;

/// <summary>
/// Archiving.ExtractArchive is the sole entry point for turning a downloaded
/// binary bundle (ffmpeg, yt-dlp, ...) into files on disk. These tests cover
/// the zip-slip containment guard: a malicious archive entry must never be
/// allowed to write outside the requested destination directory.
/// </summary>
public class ArchivingTests : IDisposable
{
    private readonly string _workDir;
    private readonly IStorage _storage;

    public ArchivingTests()
    {
        _workDir = Path.Combine(path1: Path.GetTempPath(), path2: "nm-archiving-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(path: _workDir);

        LocalStorageDriver driver = new();
        _storage = new LocalStorage(driver: driver, guard: new(allowedRoots: [], driver: driver));
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _workDir))
            Directory.Delete(path: _workDir, recursive: true);
    }

    private static void WriteZipWithEntry(string zipPath, string entryName, string content)
    {
        using FileStream fileStream = File.Create(path: zipPath);
        using ZipArchive archive = new(stream: fileStream, mode: ZipArchiveMode.Create);
        ZipArchiveEntry entry = archive.CreateEntry(entryName: entryName);
        using StreamWriter writer = new(stream: entry.Open());
        writer.Write(value: content);
    }

    [Fact]
    public async Task ExtractArchive_ZipWithRelativeTraversalEntry_Throws()
    {
        string zipPath = Path.Combine(path1: _workDir, path2: "malicious.zip");
        string destination = Path.Combine(path1: _workDir, path2: "dest");
        Directory.CreateDirectory(path: destination);

        WriteZipWithEntry(zipPath: zipPath, entryName: "../evil.txt", content: "payload");

        await Assert.ThrowsAsync<Exception>(testCode: () =>
            Archiving.ExtractArchive(storage: _storage, filePath: zipPath, destination: destination)
        );

        Assert.False(condition: File.Exists(path: Path.Combine(path1: _workDir, path2: "evil.txt")));
    }

    [Fact]
    public async Task ExtractArchive_ZipWithDeepRelativeTraversalEntry_Throws()
    {
        string zipPath = Path.Combine(path1: _workDir, path2: "malicious-deep.zip");
        string destination = Path.Combine(path1: _workDir, path2: "dest2");
        Directory.CreateDirectory(path: destination);

        WriteZipWithEntry(zipPath: zipPath, entryName: "..\\evil.txt", content: "payload");

        await Assert.ThrowsAsync<Exception>(testCode: () =>
            Archiving.ExtractArchive(storage: _storage, filePath: zipPath, destination: destination)
        );
    }

    [Fact]
    public async Task ExtractArchive_ZipWithAbsolutePathEntry_Throws()
    {
        string zipPath = Path.Combine(path1: _workDir, path2: "malicious-abs.zip");
        string destination = Path.Combine(path1: _workDir, path2: "dest3");
        Directory.CreateDirectory(path: destination);

        string outsideAbsolute = Path.Combine(path1: _workDir, path2: "outside", path3: "evil.txt");
        WriteZipWithEntry(zipPath: zipPath, entryName: outsideAbsolute, content: "payload");

        await Assert.ThrowsAsync<Exception>(testCode: () =>
            Archiving.ExtractArchive(storage: _storage, filePath: zipPath, destination: destination)
        );

        Assert.False(condition: File.Exists(path: outsideAbsolute));
    }

    [Fact]
    public async Task ExtractArchive_ZipWithNormalEntry_ExtractsIntoDestination()
    {
        string zipPath = Path.Combine(path1: _workDir, path2: "normal.zip");
        string destination = Path.Combine(path1: _workDir, path2: "dest4");
        Directory.CreateDirectory(path: destination);

        WriteZipWithEntry(zipPath: zipPath, entryName: "readme.txt", content: "hello world");

        List<string> extracted = await Archiving.ExtractArchive(storage: _storage, filePath: zipPath, destination: destination);

        Assert.Single(collection: extracted);
        string extractedPath = Path.Combine(path1: destination, path2: "readme.txt");
        Assert.True(condition: File.Exists(path: extractedPath));
        Assert.Equal(expected: "hello world", actual: await File.ReadAllTextAsync(path: extractedPath));
    }

    [Fact]
    public async Task ExtractArchive_TgzFile_RoutesToTarBranchAndExtracts()
    {
        // Regression guard for the missing-leading-dot suffix bug: ".tgz" must
        // route to the tar branch (not "unsupported format") and actually
        // extract via the real `tar` binary.
        string sourceDir = Path.Combine(path1: _workDir, path2: "tgz-source");
        Directory.CreateDirectory(path: sourceDir);
        await File.WriteAllTextAsync(path: Path.Combine(path1: sourceDir, path2: "hello.txt"), contents: "hello tgz");

        string tgzPath = Path.Combine(path1: _workDir, path2: "bundle.tgz");
        await using (FileStream fileStream = File.Create(path: tgzPath))
        await using (
            GZipStream gzipStream = new(stream: fileStream, mode: CompressionMode.Compress, leaveOpen: true)
        )
        {
            await TarFile.CreateFromDirectoryAsync(
                sourceDirectoryName: sourceDir,
                destination: gzipStream,
                includeBaseDirectory: false
            );
        }

        string destination = Path.Combine(path1: _workDir, path2: "dest-tgz");
        Directory.CreateDirectory(path: destination);

        List<string> extracted = await Archiving.ExtractArchive(storage: _storage, filePath: tgzPath, destination: destination);

        Assert.Contains(collection: extracted, filter: path => path.EndsWith(value: "hello.txt"));
        Assert.Equal(
            expected: "hello tgz",
            actual: await File.ReadAllTextAsync(path: Path.Combine(path1: destination, path2: "hello.txt"))
        );
    }

    [Fact]
    public async Task ExtractArchive_TgzFile_DestinationDirDoesNotExist_CreatesItAndExtracts()
    {
        // Regression guard: tar's `-C` target is not auto-created by the tar CLI
        // itself. On a fresh install the ffmpeg output folder is absent, so this
        // must create it before shelling out — otherwise tar aborts with
        // "Cannot open: No such file or directory" and strands BootStage.Binaries.
        string sourceDir = Path.Combine(path1: _workDir, path2: "tgz-nodest-source");
        Directory.CreateDirectory(path: sourceDir);
        await File.WriteAllTextAsync(path: Path.Combine(path1: sourceDir, path2: "hello.txt"), contents: "hello tgz");

        string tgzPath = Path.Combine(path1: _workDir, path2: "bundle-nodest.tgz");
        await using (FileStream fileStream = File.Create(path: tgzPath))
        await using (
            GZipStream gzipStream = new(stream: fileStream, mode: CompressionMode.Compress, leaveOpen: true)
        )
        {
            await TarFile.CreateFromDirectoryAsync(
                sourceDirectoryName: sourceDir,
                destination: gzipStream,
                includeBaseDirectory: false
            );
        }

        // Nested, non-existent destination — neither "dest-nodest" nor its
        // "leaf" child exist before extraction.
        string destination = Path.Combine(path1: _workDir, path2: "dest-nodest", path3: "leaf");
        Assert.False(condition: Directory.Exists(path: destination));

        List<string> extracted = await Archiving.ExtractArchive(storage: _storage, filePath: tgzPath, destination: destination);

        Assert.True(condition: Directory.Exists(path: destination));
        Assert.Contains(collection: extracted, filter: path => path.EndsWith(value: "hello.txt"));
        Assert.Equal(
            expected: "hello tgz",
            actual: await File.ReadAllTextAsync(path: Path.Combine(path1: destination, path2: "hello.txt"))
        );
    }

    [Fact]
    public async Task ExtractArchive_UnsupportedExtension_ReturnsEmptyList()
    {
        string filePath = Path.Combine(path1: _workDir, path2: "not-an-archive.txt");
        await File.WriteAllTextAsync(path: filePath, contents: "not an archive");
        string destination = Path.Combine(path1: _workDir, path2: "dest5");
        Directory.CreateDirectory(path: destination);

        List<string> extracted = await Archiving.ExtractArchive(storage: _storage, filePath: filePath, destination: destination);

        Assert.Empty(collection: extracted);
    }

    // -------------------------------------------------------------------------
    // Missing/incomplete archive guard: extraction must never be attempted
    // against a file that isn't actually (fully) on disk. Reproduces the
    // onboarding blocker where a failed/partial ffmpeg download reached `tar`
    // as a bare "No such file or directory" instead of a clear abort.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExtractArchive_MissingTarFile_ThrowsFileNotFoundWithoutInvokingTar()
    {
        string tarPath = Path.Combine(path1: _workDir, path2: "does-not-exist.tar.gz");
        string destination = Path.Combine(path1: _workDir, path2: "dest-missing-tar");
        Directory.CreateDirectory(path: destination);

        FileNotFoundException ex = await Assert.ThrowsAsync<FileNotFoundException>(testCode: () =>
            Archiving.ExtractArchive(storage: _storage, filePath: tarPath, destination: destination)
        );

        Assert.Contains(expectedSubstring: "missing or empty", actualString: ex.Message);
        Assert.Empty(collection: Directory.EnumerateFileSystemEntries(path: destination));
    }

    [Fact]
    public async Task ExtractArchive_MissingZipFile_ThrowsFileNotFoundWithoutInvokingZipFile()
    {
        string zipPath = Path.Combine(path1: _workDir, path2: "does-not-exist.zip");
        string destination = Path.Combine(path1: _workDir, path2: "dest-missing-zip");
        Directory.CreateDirectory(path: destination);

        FileNotFoundException ex = await Assert.ThrowsAsync<FileNotFoundException>(testCode: () =>
            Archiving.ExtractArchive(storage: _storage, filePath: zipPath, destination: destination)
        );

        Assert.Contains(expectedSubstring: "missing or empty", actualString: ex.Message);
        Assert.Empty(collection: Directory.EnumerateFileSystemEntries(path: destination));
    }

    [Fact]
    public async Task ExtractArchive_ZeroByteTarFile_ThrowsFileNotFoundWithoutInvokingTar()
    {
        // A 0-byte file is what a killed/partial download or a verify step that
        // deleted-then-recreated an empty placeholder would leave behind — it
        // must never be handed to `tar`, which fails with an opaque exit code.
        string tarPath = Path.Combine(path1: _workDir, path2: "empty.tar.gz");
        await File.WriteAllBytesAsync(path: tarPath, bytes: []);
        string destination = Path.Combine(path1: _workDir, path2: "dest-empty-tar");
        Directory.CreateDirectory(path: destination);

        FileNotFoundException ex = await Assert.ThrowsAsync<FileNotFoundException>(testCode: () =>
            Archiving.ExtractArchive(storage: _storage, filePath: tarPath, destination: destination)
        );

        Assert.Contains(expectedSubstring: "missing or empty", actualString: ex.Message);
        Assert.Empty(collection: Directory.EnumerateFileSystemEntries(path: destination));
    }
}
