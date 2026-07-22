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
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Subtitles;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Encoder.Subtitles;

/// <summary>
/// Tests <see cref="TesseractModelManager"/> against a fake
/// <see cref="ITesseractModelDownloader"/> — the real signed-release verification lives in
/// NoMercy.Setup's TesseractModelDownloaderTests; this class asserts what the manager does
/// with the downloader's outcome (file written / not written / cleaned up), never that a
/// method was merely called.
/// </summary>
public class TesseractModelManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly EncoderOptions _options;

    public TesseractModelManagerTests()
    {
        _tempDir = Path.Combine(path1: Path.GetTempPath(), path2: $"TessTest_{Guid.NewGuid():N}");
        _options = new() { FfmpegPathOverride = "ffmpeg", TesseractModelsDirectory = _tempDir };
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _tempDir))
            Directory.Delete(path: _tempDir, recursive: true);
        GC.SuppressFinalize(obj: this);
    }

    [Fact]
    public async Task Ensure_WhenModelExists_ReturnsPathWithoutNetwork()
    {
        Directory.CreateDirectory(path: _tempDir);
        string localPath = Path.Combine(path1: _tempDir, path2: "eng.traineddata");
        await File.WriteAllBytesAsync(path: localPath, bytes: [0xFF, 0xFE]);

        FakeTesseractModelDownloader downloader = new();
        TesseractModelManager manager = BuildManager(downloader: downloader);

        string resolved = await manager.EnsureLanguageModelAsync(language: "eng", ct: CancellationToken.None);

        Assert.Equal(expected: localPath, actual: resolved);
        Assert.Equal(expected: 0, actual: downloader.CallCount);
    }

    [Fact]
    public async Task Ensure_WhenModelMissing_DownloadsVerifiedModelAndSaves()
    {
        byte[] payload = [0x01, 0x02, 0x03, 0x04];
        FakeTesseractModelDownloader downloader = new() { Payload = payload };
        TesseractModelManager manager = BuildManager(downloader: downloader);

        string resolved = await manager.EnsureLanguageModelAsync(language: "fra", ct: CancellationToken.None);

        string expectedPath = Path.Combine(path1: _tempDir, path2: "fra.traineddata");
        Assert.Equal(expected: expectedPath, actual: resolved);
        Assert.Equal(expected: 1, actual: downloader.CallCount);
        Assert.True(condition: File.Exists(path: expectedPath));
        Assert.Equal(expected: payload, actual: await File.ReadAllBytesAsync(path: expectedPath));
        Assert.False(condition: File.Exists(path: $"{expectedPath}.tmp"));
    }

    [Fact]
    public async Task Ensure_WhenManifestSignatureInvalid_RejectsAndWritesNothing()
    {
        // The downloader hard-fails when the signed manifest's signature does not verify —
        // the manager must not write anything, and must not fall back to any other source.
        FakeTesseractModelDownloader downloader = new()
        {
            FailureToThrow = new InvalidOperationException(
                message: "nomercy-tesseract release manifest signature could not be verified"
            ),
        };
        TesseractModelManager manager = BuildManager(downloader: downloader);

        await Assert.ThrowsAsync<InvalidOperationException>(testCode: () =>
            manager.EnsureLanguageModelAsync(language: "deu", ct: CancellationToken.None)
        );

        string expectedPath = Path.Combine(path1: _tempDir, path2: "deu.traineddata");
        Assert.False(condition: File.Exists(path: expectedPath));
        Assert.False(condition: File.Exists(path: $"{expectedPath}.tmp"));
    }

    [Fact]
    public async Task Ensure_WhenShaMismatch_RejectsAndWritesNothing()
    {
        // The downloader hard-fails when the downloaded bytes don't match the signed
        // manifest's SHA-256 — the manager must not install the tampered/corrupt model.
        FakeTesseractModelDownloader downloader = new()
        {
            FailureToThrow = new InvalidDataException(
                message: "SHA-256 mismatch: the downloaded model does not match the signed manifest."
            ),
        };
        TesseractModelManager manager = BuildManager(downloader: downloader);

        await Assert.ThrowsAsync<InvalidDataException>(testCode: () =>
            manager.EnsureLanguageModelAsync(language: "jpn", ct: CancellationToken.None)
        );

        string expectedPath = Path.Combine(path1: _tempDir, path2: "jpn.traineddata");
        Assert.False(condition: File.Exists(path: expectedPath));
        Assert.False(condition: File.Exists(path: $"{expectedPath}.tmp"));
    }

    [Fact]
    public async Task Ensure_EmptyLanguage_ThrowsArgumentException()
    {
        TesseractModelManager manager = BuildManager(downloader: new FakeTesseractModelDownloader());

        await Assert.ThrowsAsync<ArgumentException>(testCode: () =>
            manager.EnsureLanguageModelAsync(language: "", ct: CancellationToken.None)
        );
    }

    [Fact]
    public void GetDownloadedLanguages_WhenDirectoryMissing_ReturnsEmpty()
    {
        TesseractModelManager manager = BuildManager(downloader: new FakeTesseractModelDownloader());

        IReadOnlyList<string> langs = manager.GetDownloadedLanguages();

        Assert.Empty(collection: langs);
    }

    [Fact]
    public async Task GetDownloadedLanguages_ListsAllTrainedData()
    {
        Directory.CreateDirectory(path: _tempDir);
        await File.WriteAllTextAsync(path: Path.Combine(path1: _tempDir, path2: "eng.traineddata"), contents: "data");
        await File.WriteAllTextAsync(path: Path.Combine(path1: _tempDir, path2: "fra.traineddata"), contents: "data");
        await File.WriteAllTextAsync(path: Path.Combine(path1: _tempDir, path2: "readme.txt"), contents: "not a model");

        TesseractModelManager manager = BuildManager(downloader: new FakeTesseractModelDownloader());

        IReadOnlyList<string> langs = manager.GetDownloadedLanguages();

        Assert.Contains(expected: "eng", collection: langs);
        Assert.Contains(expected: "fra", collection: langs);
        Assert.DoesNotContain(expected: "readme", collection: langs);
    }

    [Fact]
    public async Task Ensure_WhenDownloadCancelled_DoesNotLeavePartialFile()
    {
        CancellationTokenSource cts = new();
        FakeTesseractModelDownloader downloader = new() { OnDownloadRequested = cts.Cancel };
        TesseractModelManager manager = BuildManager(downloader: downloader);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(testCode: () =>
            manager.EnsureLanguageModelAsync(language: "deu", ct: cts.Token)
        );

        string expectedPath = Path.Combine(path1: _tempDir, path2: "deu.traineddata");
        Assert.False(condition: File.Exists(path: expectedPath));
        Assert.False(condition: File.Exists(path: $"{expectedPath}.tmp"));
    }

    private TesseractModelManager BuildManager(ITesseractModelDownloader downloader)
    {
        LocalStorageDriver driver = new();
        LocalStorage storage = new(driver: driver, guard: new(allowedRoots: [], driver: driver));
        return new(options: _options, downloader: downloader, storage: storage, logger: NullLogger<TesseractModelManager>.Instance);
    }

    private sealed class FakeTesseractModelDownloader : ITesseractModelDownloader
    {
        public byte[] Payload { get; init; } = [];
        public Exception? FailureToThrow { get; init; }
        public Action? OnDownloadRequested { get; init; }
        public int CallCount { get; private set; }

        public Task<Stream> DownloadVerifiedAsync(string language, CancellationToken ct)
        {
            CallCount++;
            OnDownloadRequested?.Invoke();
            ct.ThrowIfCancellationRequested();

            if (FailureToThrow is not null)
                throw FailureToThrow;

            return Task.FromResult<Stream>(result: new MemoryStream(buffer: Payload));
        }
    }
}
