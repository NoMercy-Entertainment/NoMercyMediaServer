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

using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.BuildingBlocks.Drm;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.NmSystem.Information;
using NoMercy.Storage;
using NoMercy.Tests.Encoder.Integration;

namespace NoMercy.Tests.Encoder.BuildingBlocks.Drm;

public class CencDrmProcessorTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static byte[] FakeKeyId() =>
        Enumerable.Range(start: 0, count: 16).Select(selector: i => (byte)(0x10 + i)).ToArray();

    private static byte[] FakeKey() =>
        Enumerable.Range(start: 0, count: 16).Select(selector: i => (byte)(0xA0 + i)).ToArray();

    private static CencKeyEntry SdEntry() => new(Label: "SD", KeyId: FakeKeyId(), Key: FakeKey());

    private static DrmConfig CencConfig(IReadOnlyList<CencKeyEntry>? keys = null) =>
        new(
            Method: DrmMethod.Cenc,
            KeyUri: "https://license.example/widevine",
            CencKeys: keys ?? [SdEntry()]
        );

    private static IStorage MakeStorage() => Mock.Of<IStorage>();

    private static CencDrmProcessor BuildSut(
        string? packagerPath = "/fake/packager",
        IProcessRunner? runner = null
    )
    {
        EncoderOptions opts = new()
        {
            FfmpegPathOverride = "/fake/ffmpeg",
            ShakaPackagerPathOverride = packagerPath,
        };
        IProcessRunner processRunner =
            runner
            ?? Mock.Of<IProcessRunner>(predicate: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ) == Task.FromResult(new ProcessResult(0, "", "", TimeSpan.Zero))
            );
        return new(
            options: opts,
            processRunner: processRunner,
            logger: NullLogger<CencDrmProcessor>.Instance,
            storage: MakeStorage()
        );
    }

    // ── BuildPackagerArguments (pure, no I/O) ─────────────────────────────

    [Fact]
    public void BuildPackagerArguments_SingleKey_ContainsRawKeyFlag()
    {
        CencKeyEntry entry = SdEntry();
        CencStreamDescriptor descriptor = new(
            InputPath: "/out/video.mp4",
            StreamType: "video",
            OutputPath: "/out/video_enc.mp4",
            DrmLabel: "SD"
        );

        List<string> args = CencDrmProcessor.BuildPackagerArguments(
            streamDescriptors: [descriptor],
            keys: [entry],
            mpdOutputPath: "/out/manifest.mpd"
        );

        args.Should().Contain(expected: "--enable_raw_key_encryption");
        args.Should().Contain(expected: "--mpd_output");
        args.Should().Contain(expected: "/out/manifest.mpd");
    }

    [Fact]
    public void BuildPackagerArguments_KeysFlag_HasCorrectHexFormat()
    {
        CencKeyEntry entry = new(Label: "HD", KeyId: new byte[16], Key: new byte[16]);
        CencStreamDescriptor descriptor = new(InputPath: "/in.mp4", StreamType: "video", OutputPath: "/out.mp4", DrmLabel: "HD");

        List<string> args = CencDrmProcessor.BuildPackagerArguments(
            streamDescriptors: [descriptor],
            keys: [entry],
            mpdOutputPath: "/manifest.mpd"
        );

        // --keys must be followed by the label:key_id:key triple
        int keysIndex = args.IndexOf(item: "--keys");
        keysIndex.Should().BeGreaterThanOrEqualTo(expected: 0, because: "--keys flag must be present");

        string keySpec = args[index: keysIndex + 1];
        keySpec.Should().StartWith(expected: "label=HD:");
        keySpec.Should().Contain(expected: ":key_id=");
        keySpec.Should().Contain(expected: ":key=");
        // 16 zero bytes → 32 zero hex chars
        keySpec.Should().Contain(expected: "key_id=00000000000000000000000000000000");
        keySpec.Should().Contain(expected: ":key=00000000000000000000000000000000");
    }

    [Fact]
    public void BuildPackagerArguments_StreamSpec_ForwardSlashes()
    {
        CencStreamDescriptor descriptor = new(
            InputPath: @"C:\out\video.mp4",
            StreamType: "video",
            OutputPath: @"C:\out\video_enc.mp4",
            DrmLabel: "SD"
        );

        List<string> args = CencDrmProcessor.BuildPackagerArguments(
            streamDescriptors: [descriptor],
            keys: [SdEntry()],
            mpdOutputPath: "/manifest.mpd"
        );

        string spec = args[index: 0];
        spec.Should().NotContain(unexpected: "\\", because: "shaka-packager requires forward slashes");
        spec.Should().StartWith(expected: "in=C:/out/video.mp4");
    }

    [Fact]
    public void BuildPackagerArguments_MultipleKeys_EmitsOneKeysEntryEach()
    {
        List<CencKeyEntry> keys =
        [
            new(Label: "SD", KeyId: FakeKeyId(), Key: FakeKey()),
            new(Label: "HD", KeyId: FakeKeyId(), Key: FakeKey()),
        ];
        CencStreamDescriptor descriptor = new(InputPath: "/v.mp4", StreamType: "video", OutputPath: "/v_enc.mp4", DrmLabel: "SD");

        List<string> args = CencDrmProcessor.BuildPackagerArguments(
            streamDescriptors: [descriptor],
            keys: keys,
            mpdOutputPath: "/manifest.mpd"
        );

        int count = args.Count(predicate: a => a == "--keys");
        count.Should().Be(expected: 2, because: "one --keys flag per key entry");
    }

    // ── PrepareAsync — must be a no-op for CENC ───────────────────────────

    [Fact]
    public async Task PrepareAsync_Cenc_ReturnsEmptySentinel()
    {
        CencDrmProcessor sut = BuildSut();
        DrmConfig config = CencConfig();

        DrmArtifact artifact = await sut.PrepareAsync(
            outputDirectory: "/tmp/drm-test",
            config: config,
            ct: CancellationToken.None
        );

        artifact.KeyInfoFilePath.Should().BeEmpty(because: "CENC pre-encode prep is a no-op");
        artifact.KeyFilePath.Should().BeEmpty();
        artifact.Key.Should().BeEmpty();
        artifact.Iv.Should().BeEmpty();
        artifact.KeyUri.Should().Be(expected: config.KeyUri);
    }

    [Fact]
    public async Task PrepareAsync_WrongMethod_ThrowsArgumentException()
    {
        CencDrmProcessor sut = BuildSut();
        DrmConfig wrongConfig = new(Method: DrmMethod.Aes128, KeyUri: "http://k");

        Func<Task> act = () =>
            sut.PrepareAsync(outputDirectory: "/tmp/drm-test", config: wrongConfig, ct: CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage(expectedWildcardPattern: "*CENC only*");
    }

    // ── PackageAsync error paths ───────────────────────────────────────────

    [Fact]
    public async Task PackageAsync_WithMissingPackager_ThrowsClearError()
    {
        // ShakaPackagerPathOverride points to a non-existent file;
        // FfmpegPathOverride sibling also won't exist → GetShakaPackagerPath() throws.
        EncoderOptions opts = new()
        {
            FfmpegPathOverride = "/nonexistent/ffmpeg",
            ShakaPackagerPathOverride = null,
        };
        CencDrmProcessor sut = new(
            options: opts,
            processRunner: Mock.Of<IProcessRunner>(),
            logger: NullLogger<CencDrmProcessor>.Instance,
            storage: MakeStorage()
        );

        Func<Task> act = () =>
            sut.PackageAsync(
                outputDirectory: "/tmp",
                streamDescriptors: [new(InputPath: "/v.mp4", StreamType: "video", OutputPath: "/v_enc.mp4", DrmLabel: "SD")],
                config: CencConfig(),
                mpdOutputPath: "/manifest.mpd",
                ct: CancellationToken.None
            );

        // GetShakaPackagerPath throws InvalidOperationException when nothing is found.
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(expectedWildcardPattern: "*shaka-packager*");
    }

    [Fact]
    public async Task PackageAsync_NonZeroExitCode_ThrowsWithDetails()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: "Fatal: bad key", Duration: TimeSpan.Zero));

        CencDrmProcessor sut = BuildSut(runner: runner.Object);

        Func<Task> act = () =>
            sut.PackageAsync(
                outputDirectory: "/tmp",
                streamDescriptors: [new(InputPath: "/v.mp4", StreamType: "video", OutputPath: "/v_enc.mp4", DrmLabel: "SD")],
                config: CencConfig(),
                mpdOutputPath: "/tmp/manifest.mpd",
                ct: CancellationToken.None
            );

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage(expectedWildcardPattern: "*shaka-packager exited with code 1*");
    }

    [Fact]
    public async Task PackageAsync_NoCencKeys_ThrowsArgumentException()
    {
        CencDrmProcessor sut = BuildSut();
        DrmConfig noKeys = new(Method: DrmMethod.Cenc, KeyUri: "https://lic.example", CencKeys: []);

        Func<Task> act = () =>
            sut.PackageAsync(
                outputDirectory: "/tmp",
                streamDescriptors: [new(InputPath: "/v.mp4", StreamType: "video", OutputPath: "/v_enc.mp4", DrmLabel: "SD")],
                config: noKeys,
                mpdOutputPath: "/tmp/manifest.mpd",
                ct: CancellationToken.None
            );

        await act.Should().ThrowAsync<ArgumentException>().WithMessage(expectedWildcardPattern: "*CencKeys*");
    }

    // ── CencStreamDescriptor ─────────────────────────────────────────────

    [Fact]
    public void CencStreamDescriptor_ToPackagerSpec_ReturnsCorrectFormat()
    {
        CencStreamDescriptor descriptor = new(
            InputPath: "/out/video.mp4",
            StreamType: "video",
            OutputPath: "/out/video_enc.mp4",
            DrmLabel: "HD"
        );

        string spec = descriptor.ToPackagerSpec();

        spec.Should().Be(expected: "in=/out/video.mp4,stream=video,output=/out/video_enc.mp4,drm_label=HD");
    }

    // ── Integration: real shaka-packager end-to-end ─────────────────────────
    // Runs when the packager binary is resolvable — via SHAKA_PACKAGER_PATH or
    // the standard binaries location (AppFiles.ShakaPackagerPath, downloaded by
    // the server's binaries step alongside ffmpeg). Skips with a clear reason
    // only when neither the packager nor the fork ffmpeg is present.

    [SkippableFact]
    public async Task PackageAsync_WithRealPackager_ProducesMpd()
    {
        string? packagerPath =
            NoMercyFfmpegProbe.ResolveShakaPackagerPath()
            ?? (File.Exists(path: AppFiles.ShakaPackagerPath) ? AppFiles.ShakaPackagerPath : null);
        Skip.If(
            condition: packagerPath is null,
            reason: "shaka-packager binary not present — set SHAKA_PACKAGER_PATH or run the "
                    + "server binaries step (downloads it alongside ffmpeg)."
        );

        string? ffmpegPath = ResolveFfmpegForFixture();
        Skip.If(condition: ffmpegPath is null, reason: "ffmpeg not resolvable for fixture generation.");

        string tmpDir = Path.Combine(path1: Path.GetTempPath(), path2: $"cenc-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: tmpDir);

        try
        {
            // 1) Generate a tiny fragmented mp4 the packager can ingest.
            string inputMp4 = Path.Combine(path1: tmpDir, path2: "input.mp4");
            await RunProcessAsync(
                fileName: ffmpegPath!,
                args:
                [
                    "-y",
                    "-f",
                    "lavfi",
                    "-i",
                    "testsrc2=size=320x180:rate=25:duration=2",
                    "-c:v",
                    "libx264",
                    "-preset",
                    "ultrafast",
                    "-pix_fmt",
                    "yuv420p",
                    inputMp4,
                ]
            );
            File.Exists(path: inputMp4).Should().BeTrue(because: "ffmpeg must produce the input clip");

            // 2) Package it with real CENC raw-key encryption via the processor.
            //    The packager writes real files, so storage.Exists must reflect the
            //    real filesystem (not the always-false default mock).
            EncoderOptions opts = new() { ShakaPackagerPathOverride = packagerPath };
            IStorage storage = Mock.Of<IStorage>(predicate: s => s.Exists(It.IsAny<string>()) == false);
            Mock.Get(mocked: storage).Setup(expression: s => s.Exists(It.IsAny<string>())).Returns<string>(valueFunction: File.Exists);
            CencDrmProcessor processor = new(
                options: opts,
                processRunner: new ProcessRunner(logger: NullLogger<ProcessRunner>.Instance),
                logger: NullLogger<CencDrmProcessor>.Instance,
                storage: storage
            );

            string manifestPath = Path.Combine(path1: tmpDir, path2: "manifest.mpd");
            CencStreamDescriptor descriptor = new(
                InputPath: inputMp4,
                StreamType: "video",
                OutputPath: Path.Combine(path1: tmpDir, path2: "video_enc.mp4"),
                DrmLabel: "SD"
            );

            string resultManifest = await processor.PackageAsync(
                outputDirectory: tmpDir,
                streamDescriptors: [descriptor],
                config: CencConfig(),
                mpdOutputPath: manifestPath,
                ct: CancellationToken.None
            );

            // 3) A real MPD with ContentProtection must have been produced.
            File.Exists(path: resultManifest).Should().BeTrue(because: "packager must produce the MPD");
            string mpd = await File.ReadAllTextAsync(path: resultManifest);
            mpd.Should().Contain(expected: "<MPD", because: "output must be a DASH manifest");
            mpd.Should().Contain(expected: "ContentProtection", because: "CENC encryption must be signalled");
        }
        finally
        {
            Directory.Delete(path: tmpDir, recursive: true);
        }
    }

    private static string? ResolveFfmpegForFixture()
    {
        string candidate = AppFiles.FfmpegPath;
        if (File.Exists(path: candidate))
            return candidate;
        return NoMercyFfmpegProbe.ResolveFfmpegPath();
    }

    private static async Task RunProcessAsync(string fileName, string[] args)
    {
        ProcessStartInfo psi = new()
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string arg in args)
            psi.ArgumentList.Add(item: arg);

        using Process process = Process.Start(startInfo: psi)!;
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(message: $"{fileName} failed: {stderr}");
    }
}
