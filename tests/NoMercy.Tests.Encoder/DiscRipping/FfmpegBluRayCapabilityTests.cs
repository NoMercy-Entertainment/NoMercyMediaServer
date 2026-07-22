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
using Moq;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Infrastructure;
using NoMercy.OpticalMedia.Capabilities;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Rip;
using NoMercy.OpticalMedia.Sources;
using NoMercy.OpticalMedia.Sources.Bluray;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Encoder.DiscRipping;

/// <summary>
/// Offline tests for AACS/BD+ capability probe and error classification.
/// All tests stub <see cref="IProcessRunner"/> — no real ffmpeg required.
/// </summary>
public class FfmpegBluRayCapabilityTests
{
    // ── FfmpegBluRayCapability.ProbeAsync ──────────────────────────────────

    [Fact]
    public async Task ProbeAsync_WhenBluRayInOutput_SetsBluRayProtocolPresent()
    {
        EncoderOptions options = new()
        {
            FfmpegPathOverride = "/usr/bin/ffmpeg",
            FfprobePathOverride = "/usr/bin/ffprobe",
        };

        Mock<IProcessRunner> runner = new();
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new ProcessResult(ExitCode: 0, StdOut: "bluray\nhls\nrtmp", StdErr: "", Duration: TimeSpan.FromMilliseconds(milliseconds: 10))
            );

        FfmpegBluRayCapability cap = new(
            options: options,
            processRunner: runner.Object,
            logger: NullLogger<FfmpegBluRayCapability>.Instance
        );

        await cap.ProbeAsync();

        cap.BluRayProtocolPresent.Should().BeTrue();
    }

    [Fact]
    public async Task ProbeAsync_WhenBluRayAbsent_BluRayProtocolPresentIsFalse()
    {
        EncoderOptions options = new()
        {
            FfmpegPathOverride = "/usr/bin/ffmpeg",
            FfprobePathOverride = "/usr/bin/ffprobe",
        };

        Mock<IProcessRunner> runner = new();
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new ProcessResult(ExitCode: 0, StdOut: "hls\nrtmp\nfile", StdErr: "", Duration: TimeSpan.FromMilliseconds(milliseconds: 10))
            );

        FfmpegBluRayCapability cap = new(
            options: options,
            processRunner: runner.Object,
            logger: NullLogger<FfmpegBluRayCapability>.Instance
        );

        await cap.ProbeAsync();

        cap.BluRayProtocolPresent.Should().BeFalse();
    }

    [Fact]
    public async Task ProbeAsync_WithKeyDbOverride_ActiveKeyDbPathReportsOverride()
    {
        EncoderOptions options = new()
        {
            FfmpegPathOverride = "/usr/bin/ffmpeg",
            FfprobePathOverride = "/usr/bin/ffprobe",
            BluRay = new() { KeyDbOverridePath = "/home/user/KEYDB.cfg" },
        };

        Mock<IProcessRunner> runner = new();
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: "bluray", StdErr: "", Duration: TimeSpan.FromMilliseconds(milliseconds: 10)));

        FfmpegBluRayCapability cap = new(
            options: options,
            processRunner: runner.Object,
            logger: NullLogger<FfmpegBluRayCapability>.Instance
        );

        await cap.ProbeAsync();

        cap.ActiveKeyDbPath.Should().Contain(expected: "/home/user/KEYDB.cfg");
    }

    [Fact]
    public async Task ProbeAsync_WithoutOverride_ActiveKeyDbPathReportsBundled()
    {
        EncoderOptions options = new()
        {
            FfmpegPathOverride = "/usr/bin/ffmpeg",
            FfprobePathOverride = "/usr/bin/ffprobe",
        };

        Mock<IProcessRunner> runner = new();
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: "bluray", StdErr: "", Duration: TimeSpan.FromMilliseconds(milliseconds: 10)));

        FfmpegBluRayCapability cap = new(
            options: options,
            processRunner: runner.Object,
            logger: NullLogger<FfmpegBluRayCapability>.Instance
        );

        await cap.ProbeAsync();

        cap.ActiveKeyDbPath.Should().Contain(expected: "bundled");
    }

    // ── DiscScanner.ClassifyBluRayStderr — each stderr pattern ────────────

    [Fact]
    public void ClassifyBluRayStderr_AacsCertMissing_ThrowsDiscAacsCertMissing()
    {
        const string stderr =
            "aacs: no matching certificate for volume AABBCCDD11223344AABBCCDD11223344\n";

        EncoderRuntimeException ex = Assert.Throws<EncoderRuntimeException>(testCode: () =>
            DiscScanner.ClassifyBluRayStderr(drivePath: "bluray:/dev/sr0", stderr: stderr)
        );

        ex.Shape.Id.Should().Be(expected: EncoderRuleId.DiscAacsCertMissing);
        ex.Shape.Message.Should().Contain(expected: "AABBCCDD11223344AABBCCDD11223344");
        ex.Shape.Suggestion.Should().Contain(expected: "KEYDB.cfg");
    }

    [Fact]
    public void ClassifyBluRayStderr_BdplusConverterMissing_ThrowsDiscBdplusConverterMissing()
    {
        const string stderr = "bdplus: no matching converter for this disc\n";

        EncoderRuntimeException ex = Assert.Throws<EncoderRuntimeException>(testCode: () =>
            DiscScanner.ClassifyBluRayStderr(drivePath: "bluray:/dev/sr0", stderr: stderr)
        );

        ex.Shape.Id.Should().Be(expected: EncoderRuleId.DiscBdplusConverterMissing);
    }

    [Fact]
    public void ClassifyBluRayStderr_ProtocolNotFound_ThrowsDiscReadError()
    {
        const string stderr = "Protocol not found\n";

        EncoderRuntimeException ex = Assert.Throws<EncoderRuntimeException>(testCode: () =>
            DiscScanner.ClassifyBluRayStderr(drivePath: "bluray:/dev/sr0", stderr: stderr)
        );

        ex.Shape.Id.Should().Be(expected: EncoderRuleId.DiscReadError);
    }

    [Fact]
    public void ClassifyBluRayStderr_EmptyStderr_DoesNotThrow()
    {
        // Empty stderr is a no-op — probe timed out or disc responded fine.
        Action act = () => DiscScanner.ClassifyBluRayStderr(drivePath: "bluray:/dev/sr0", stderr: "");

        act.Should().NotThrow();
    }

    [Fact]
    public void ClassifyBluRayStderr_UnknownStderr_DoesNotThrow()
    {
        // Unknown stderr is not classified — the caller falls through to generic handling.
        Action act = () =>
            DiscScanner.ClassifyBluRayStderr(drivePath: "bluray:/dev/sr0", stderr: "some random ffprobe output\n");

        act.Should().NotThrow();
    }

    // ── Env var forwarding ─────────────────────────────────────────────────

    [Fact]
    public async Task DiscRipper_WithKeyDbOverride_ForwardsLibaacsKeyDbEnvVar()
    {
        EncoderOptions options = new()
        {
            FfmpegPathOverride = "/usr/bin/ffmpeg",
            FfprobePathOverride = "/usr/bin/ffprobe",
            BluRay = new() { KeyDbOverridePath = "/mnt/keys/KEYDB.cfg" },
        };

        IReadOnlyDictionary<string, string>? capturedEnv = null;
        Mock<IProcessRunner> runner = new();

        // Capture calls via the extraEnv overload.
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<IReadOnlyDictionary<string, string>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<
                string,
                string[],
                IReadOnlyDictionary<string, string>?,
                string?,
                CancellationToken
            >(action: (_, _, env, _, _) => capturedEnv = env)
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.FromSeconds(seconds: 1)));

        // Also stub the standard overload (used when no env overrides).
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.FromSeconds(seconds: 1)));

        string outputDir = Path.Combine(path1: Path.GetTempPath(), path2: $"Rip_{Guid.NewGuid():N}");
        try
        {
            LocalStorageDriver driver = new();
            LocalStorage storage = new(driver: driver, guard: new(allowedRoots: [], driver: driver));
            DriveLockRegistry lockRegistry = new();
            DiscRipper ripper = new(
                options: options,
                processRunner: runner.Object,
                storage: storage,
                driveLockRegistry: lockRegistry,
                logger: NullLogger<DiscRipper>.Instance
            );

            RipRequest request = new(
                DrivePath: "bluray:/dev/sr0",
                SelectedTitleIndices: [0],
                MetadataId: null,
                Custom: null,
                LibraryId: Ulid.NewUlid(),
                FolderId: Ulid.NewUlid(),
                EncodingProfileId: null,
                AudioTracks: [new(StreamIndex: 0, Include: true)],
                Subtitles: []
            );

            await ripper.RipAsync(request: request, outputDirectory: outputDir, ct: CancellationToken.None);

            capturedEnv.Should().NotBeNull();
            capturedEnv!.Should().ContainKey(expected: "LIBAACS_KEY_DB");
            capturedEnv[key: "LIBAACS_KEY_DB"].Should().Be(expected: "/mnt/keys/KEYDB.cfg");
        }
        finally
        {
            if (Directory.Exists(path: outputDir))
                Directory.Delete(path: outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task DiscRipper_WithBdPlusOverride_ForwardsLibbdplusDatabaseEnvVar()
    {
        EncoderOptions options = new()
        {
            FfmpegPathOverride = "/usr/bin/ffmpeg",
            FfprobePathOverride = "/usr/bin/ffprobe",
            BluRay = new() { AacsKeysOverridePath = "/mnt/keys/bdplus/" },
        };

        IReadOnlyDictionary<string, string>? capturedEnv = null;
        Mock<IProcessRunner> runner = new();

        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<IReadOnlyDictionary<string, string>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<
                string,
                string[],
                IReadOnlyDictionary<string, string>?,
                string?,
                CancellationToken
            >(action: (_, _, env, _, _) => capturedEnv = env)
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.FromSeconds(seconds: 1)));

        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.FromSeconds(seconds: 1)));

        string outputDir = Path.Combine(path1: Path.GetTempPath(), path2: $"Rip_{Guid.NewGuid():N}");
        try
        {
            LocalStorageDriver driver = new();
            LocalStorage storage = new(driver: driver, guard: new(allowedRoots: [], driver: driver));
            DriveLockRegistry lockRegistry = new();
            DiscRipper ripper = new(
                options: options,
                processRunner: runner.Object,
                storage: storage,
                driveLockRegistry: lockRegistry,
                logger: NullLogger<DiscRipper>.Instance
            );

            RipRequest request = new(
                DrivePath: "bluray:/dev/sr0",
                SelectedTitleIndices: [0],
                MetadataId: null,
                Custom: null,
                LibraryId: Ulid.NewUlid(),
                FolderId: Ulid.NewUlid(),
                EncodingProfileId: null,
                AudioTracks: [new(StreamIndex: 0, Include: true)],
                Subtitles: []
            );

            await ripper.RipAsync(request: request, outputDirectory: outputDir, ct: CancellationToken.None);

            capturedEnv.Should().NotBeNull();
            capturedEnv!.Should().ContainKey(expected: "LIBBDPLUS_DATABASE");
            capturedEnv[key: "LIBBDPLUS_DATABASE"].Should().Be(expected: "/mnt/keys/bdplus/");
        }
        finally
        {
            if (Directory.Exists(path: outputDir))
                Directory.Delete(path: outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task DiscRipper_WithoutBluRayOptions_UsesStandardRunAsync()
    {
        EncoderOptions options = new()
        {
            FfmpegPathOverride = "/usr/bin/ffmpeg",
            FfprobePathOverride = "/usr/bin/ffprobe",
            // BluRay is null — no overrides
        };

        bool standardOverloadCalled = false;
        Mock<IProcessRunner> runner = new();

        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, string[], string?, CancellationToken>(
                action: (_, _, _, _) => standardOverloadCalled = true
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.FromSeconds(seconds: 1)));

        string outputDir = Path.Combine(path1: Path.GetTempPath(), path2: $"Rip_{Guid.NewGuid():N}");
        try
        {
            LocalStorageDriver driver = new();
            LocalStorage storage = new(driver: driver, guard: new(allowedRoots: [], driver: driver));
            DriveLockRegistry lockRegistry = new();
            DiscRipper ripper = new(
                options: options,
                processRunner: runner.Object,
                storage: storage,
                driveLockRegistry: lockRegistry,
                logger: NullLogger<DiscRipper>.Instance
            );

            RipRequest request = new(
                DrivePath: "bluray:/dev/sr0",
                SelectedTitleIndices: [0],
                MetadataId: null,
                Custom: null,
                LibraryId: Ulid.NewUlid(),
                FolderId: Ulid.NewUlid(),
                EncodingProfileId: null,
                AudioTracks: [new(StreamIndex: 0, Include: true)],
                Subtitles: []
            );

            await ripper.RipAsync(request: request, outputDirectory: outputDir, ct: CancellationToken.None);

            standardOverloadCalled.Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(path: outputDir))
                Directory.Delete(path: outputDir, recursive: true);
        }
    }
}
