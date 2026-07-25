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
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies;
using NoMercy.Encoder.Strategies.Dash;
using NoMercy.Encoder.Strategies.Mkv;
using NoMercy.Encoder.Strategies.Mp4;
using NoMercy.Tests.Encoder.Storage;
using Container = NoMercy.Encoder.Profiles.Container;

namespace NoMercy.Tests.Encoder.Strategies;

public class FormatStrategiesTests
{
    [Theory]
    [MemberData(nameof(StrategyExpectations))]
    public void Strategy_ExposesExpectedFormatAndMode(
        IEncodingStrategy strategy,
        OutputFormat expectedFormat,
        EncodeMode expectedMode
    )
    {
        Assert.Equal(expectedFormat, strategy.Format);
        Assert.Equal(expectedMode, strategy.EncodeMode);
    }

    [Theory]
    [MemberData(nameof(Strategies))]
    public async Task Strategy_DelegatesToInjectedEncoder(IEncodingStrategy strategy)
    {
        EncodingRequest request = FakeRequest();
        EncodingResult? result = await strategy.EncodeAsync(
            request,
            null,
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.True(result.Success);
    }

    public static TheoryData<IEncodingStrategy, OutputFormat, EncodeMode> StrategyExpectations()
    {
        IEncoder encoder = BuildMockEncoder();
        return new()
        {
            {
                new MkvStrategy(
                    encoder,
                    NullLogger<MkvStrategy>.Instance,
                    TestStorageFactory.CreateLocal()
                ),
                OutputFormat.Mkv,
                EncodeMode.SinglePass
            },
            {
                new Mp4SinglePassStrategy(
                    encoder,
                    NullLogger<Mp4SinglePassStrategy>.Instance,
                    TestStorageFactory.CreateLocal()
                ),
                OutputFormat.Mp4,
                EncodeMode.SinglePass
            },
            {
                new DashSinglePassStrategy(
                    encoder,
                    NullLogger<DashSinglePassStrategy>.Instance,
                    TestStorageFactory.CreateLocal()
                ),
                OutputFormat.Dash,
                EncodeMode.SinglePass
            },
        };
    }

    public static TheoryData<IEncodingStrategy> Strategies()
    {
        IEncoder encoder = BuildMockEncoder();
        return new()
        {
            new MkvStrategy(
                encoder,
                NullLogger<MkvStrategy>.Instance,
                TestStorageFactory.CreateLocal()
            ),
            new Mp4SinglePassStrategy(
                encoder,
                NullLogger<Mp4SinglePassStrategy>.Instance,
                TestStorageFactory.CreateLocal()
            ),
            new DashSinglePassStrategy(
                encoder,
                NullLogger<DashSinglePassStrategy>.Instance,
                TestStorageFactory.CreateLocal()
            ),
        };
    }

    private static IEncoder BuildMockEncoder()
    {
        Mock<IEncoder> mock = new();
        mock.Setup(e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new EncodingResult(
                    true,
                    "/out",
                    TimeSpan.Zero,
                    null,
                    new(0, 0, 0, "test", null)
                )
            );
        return mock.Object;
    }

    private static EncodingRequest FakeRequest() =>
        new(
            "/media/test.mkv",
            "/out",
            new(
                Ulid.NewUlid(),
                "Test",
                Container.HlsTs,
                null,
                [],
                []
            )
        );
}
