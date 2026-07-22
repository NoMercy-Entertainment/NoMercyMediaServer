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
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;

namespace NoMercy.Tests.Encoder.Hardware;

public class DriverChangeDetectorTests
{
    private static IReadOnlyList<GpuDevice> OneGpu(string? driver = "31.0.15.4601") =>
        [
            new(
                Vendor: GpuVendor.Nvidia,
                Name: "RTX 4090",
                VramMb: 24576,
                MaxEncoderSessions: 8,
                SupportedCodecs: [VideoCodecType.H264, VideoCodecType.H265],
                DriverVersion: driver
            ),
        ];

    [Fact]
    public async Task DetectAndPersistAsync_FirstBoot_ReturnsIsFirstBootTrueChangedFalse()
    {
        Mock<IHardwareDetector> detector = new();
        detector
            .Setup(expression: d => d.DetectGpusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: OneGpu());

        Mock<IDriverFingerprintStore> store = new();
        store
            .Setup(expression: s => s.LoadHashAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (string?)null);
        store
            .Setup(expression: s => s.SaveHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(value: Task.CompletedTask);

        DriverChangeDetector sut = new(hardwareDetector: detector.Object, store: store.Object);

        DriverChangeResult result = await sut.DetectAndPersistAsync();

        result.IsFirstBoot.Should().BeTrue();
        result.Changed.Should().BeFalse();
        result.PreviousHash.Should().BeNull();
        result.CurrentHash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task DetectAndPersistAsync_DifferentHash_ReturnsChanged()
    {
        Mock<IHardwareDetector> detector = new();
        detector
            .Setup(expression: d => d.DetectGpusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: OneGpu(driver: "31.0.15.5000"));

        Mock<IDriverFingerprintStore> store = new();
        store
            .Setup(expression: s => s.LoadHashAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: "aabbccdd00112233aabbccdd00112233aabbccdd00112233aabbccdd00112233");
        store
            .Setup(expression: s => s.SaveHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(value: Task.CompletedTask);

        DriverChangeDetector sut = new(hardwareDetector: detector.Object, store: store.Object);

        DriverChangeResult result = await sut.DetectAndPersistAsync();

        result.Changed.Should().BeTrue();
        result.IsFirstBoot.Should().BeFalse();
    }

    [Fact]
    public async Task DetectAndPersistAsync_SameHash_ReturnsNotChanged()
    {
        IReadOnlyList<GpuDevice> gpus = OneGpu();

        Mock<IHardwareDetector> detector = new();
        detector.Setup(expression: d => d.DetectGpusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(value: gpus);

        // Compute the expected hash so the stored hash matches
        DriverFingerprint fp = new(Gpus: [new(Vendor: "Nvidia", Model: "RTX 4090", DriverVersion: "31.0.15.4601", Index: 0)]);
        string expectedHash = fp.ComputeHash();

        Mock<IDriverFingerprintStore> store = new();
        store.Setup(expression: s => s.LoadHashAsync(It.IsAny<CancellationToken>())).ReturnsAsync(value: expectedHash);
        store
            .Setup(expression: s => s.SaveHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(value: Task.CompletedTask);

        DriverChangeDetector sut = new(hardwareDetector: detector.Object, store: store.Object);

        DriverChangeResult result = await sut.DetectAndPersistAsync();

        result.Changed.Should().BeFalse();
        result.IsFirstBoot.Should().BeFalse();
    }

    [Fact]
    public async Task DetectAndPersistAsync_AlwaysPersistsCurrentHash()
    {
        Mock<IHardwareDetector> detector = new();
        detector
            .Setup(expression: d => d.DetectGpusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: OneGpu());

        Mock<IDriverFingerprintStore> store = new();
        store
            .Setup(expression: s => s.LoadHashAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (string?)null);

        string? savedHash = null;
        store
            .Setup(expression: s => s.SaveHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>(action: (h, _) => savedHash = h)
            .Returns(value: Task.CompletedTask);

        DriverChangeDetector sut = new(hardwareDetector: detector.Object, store: store.Object);

        DriverChangeResult result = await sut.DetectAndPersistAsync();

        store.Verify(
            expression: s => s.SaveHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            times: Times.Once
        );
        savedHash.Should().Be(expected: result.CurrentHash);
    }
}
