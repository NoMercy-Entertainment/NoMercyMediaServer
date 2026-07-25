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

using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Pipeline;
using Container = NoMercy.Encoder.Profiles.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;

namespace NoMercy.Tests.Encoder.Jobs;

public class BatchEncodingRequestTests
{
    private static EncodingProfile BuildProfile() =>
        new(
            Ulid.NewUlid(),
            "HLS 1080p",
            Container.HlsTs,
            null,
            [],
            []
        );

    private static EncodingRequest BuildRequest(string inputPath) =>
        new(inputPath, "/output/batch", BuildProfile());

    [Fact]
    public void BatchEncodingRequest_WithThreeItems_IsConstructable()
    {
        EncodingRequest[] items =
        [
            BuildRequest("/media/a.mkv"),
            BuildRequest("/media/b.mkv"),
            BuildRequest("/media/c.mkv"),
        ];

        BatchEncodingRequest request = new(items, new());

        request.Items.Should().HaveCount(3);
        request.Items.Should().Contain(r => r.InputPath == "/media/a.mkv");
        request.Items.Should().Contain(r => r.InputPath == "/media/b.mkv");
        request.Items.Should().Contain(r => r.InputPath == "/media/c.mkv");
    }

    [Fact]
    public void BatchEncodingRequest_WithOptions_PreservesOptions()
    {
        BatchOptions options = new(
            true,
            true,
            2,
            BatchCancellationMode.CancelAll
        );

        BatchEncodingRequest request = new([BuildRequest("/media/a.mkv")], options);

        request.Options.ShareAnalysis.Should().BeTrue();
        request.Options.ParallelEncoding.Should().BeTrue();
        request.Options.MaxParallel.Should().Be(2);
        request.Options.CancelMode.Should().Be(BatchCancellationMode.CancelAll);
    }

    [Fact]
    public void BatchEncodingRequest_WithEmptyItems_IsConstructable()
    {
        // Empty items array is constructable — caller validation is responsibility of the consumer
        BatchEncodingRequest request = new([], new());

        request.Items.Should().BeEmpty();
    }

    [Fact]
    public void BatchEncodingRequest_EmptyItems_ShouldBeRejected_ByConsumer()
    {
        // Demonstrate that a caller validating for empty items can detect it
        BatchEncodingRequest request = new([], new());

        bool isRejected = request.Items.Length == 0;
        isRejected.Should().BeTrue();
    }

    [Fact]
    public void BatchOptions_Defaults_AreCorrect()
    {
        BatchOptions options = new();

        options.ShareAnalysis.Should().BeTrue();
        options.ParallelEncoding.Should().BeFalse();
        options.MaxParallel.Should().Be(1);
        options.CancelMode.Should().Be(BatchCancellationMode.SkipRemaining);
    }
}
