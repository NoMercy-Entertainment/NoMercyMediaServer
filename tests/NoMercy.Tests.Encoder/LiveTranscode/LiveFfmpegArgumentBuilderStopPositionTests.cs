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

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.LiveTranscode;

namespace NoMercy.Tests.Encoder.LiveTranscode;

/// <summary>
/// Coverage for <c>LiveRunInput.StopPosition</c> in <see cref="LiveFfmpegArgumentBuilder"/> —
/// the "-t" emission that lets a gap-bounded respawn stop instead of re-encoding
/// straight through to EOF over already-transcoded content.
/// </summary>
public class LiveFfmpegArgumentBuilderStopPositionTests
{
    private static LiveQuality MakeQuality() =>
        new(
            Id: "1080p",
            Label: "1080p",
            Width: 1920,
            Height: 1080,
            Codec: VideoCodecType.H264,
            BitrateKbps: 8000,
            Encoder: "libx264",
            IsHardwareAccelerated: false,
            ExpectedSpeed: 2.0,
            CanRealtime: true
        );

    [Fact]
    public void StopPositionNull_OmitsDashT_AndBoundingChangesNothingElse()
    {
        // Removing the inserted pair from the bounded argv must reproduce the
        // unbounded one token-for-token: bounding a run may add "-t" and nothing
        // else, so the unbounded path every existing session takes is unchanged.
        LiveRunInput baseInput = new(
            InputPath: "/media/in.mkv",
            OutputDirectory: "/tmp/live",
            StartPosition: TimeSpan.FromSeconds(seconds: 120),
            Quality: MakeQuality(),
            SegmentDurationSeconds: 6
        );
        LiveRunInput unbounded = baseInput with { StopPosition = null };
        LiveRunInput bounded = baseInput with { StopPosition = TimeSpan.FromSeconds(seconds: 180) };

        string[] unboundedArgs = LiveFfmpegArgumentBuilder.Build(input: unbounded);
        string[] boundedArgs = LiveFfmpegArgumentBuilder.Build(input: bounded);

        unboundedArgs.Should().NotContain(unexpected: "-t");

        int tIdx = Array.IndexOf(array: boundedArgs, value: "-t");
        tIdx.Should().BeGreaterThanOrEqualTo(expected: 0);

        List<string> boundedWithoutDashT = [.. boundedArgs];
        boundedWithoutDashT.RemoveRange(index: tIdx, count: 2); // "-t" and its value

        boundedWithoutDashT.Should().Equal(expected: unboundedArgs);
    }

    [Fact]
    public void StopPositionSet_EmitsDashTWithPreOffsetDuration_BeforeOutputTsOffset()
    {
        // Start segment index 20 (120s / 6s) — offset is 120s. Stop at 180s means
        // the pre-offset duration ffmpeg must run for is 180 - 120 = 60s.
        LiveRunInput input = new(
            InputPath: "/media/in.mkv",
            OutputDirectory: "/tmp/live",
            StartPosition: TimeSpan.FromSeconds(seconds: 120),
            Quality: MakeQuality(),
            SegmentDurationSeconds: 6,
            StopPosition: TimeSpan.FromSeconds(seconds: 180)
        );

        string[] args = LiveFfmpegArgumentBuilder.Build(input: input);

        int tIdx = Array.IndexOf(array: args, value: "-t");
        tIdx.Should().BeGreaterThanOrEqualTo(expected: 0);
        args[tIdx + 1].Should().Be(expected: "60.000");

        int offsetIdx = Array.IndexOf(array: args, value: "-output_ts_offset");
        offsetIdx.Should().BeGreaterThanOrEqualTo(expected: 0);
        tIdx.Should().BeLessThan(expected: offsetIdx, because: "-t must be emitted before -output_ts_offset");

        // The absolute start index is unaffected by bounding the run.
        int startNumberIdx = Array.IndexOf(array: args, value: "-start_number");
        args[startNumberIdx + 1].Should().Be(expected: "20");
    }

    [Fact]
    public void StopPositionAtOrBeforeStart_EmitsNoDashT()
    {
        // StartPosition is already an exact segment-boundary multiple (60s / 6s =
        // index 10, offset 60s exactly), so a StopPosition at or before it yields
        // a non-positive duration — must never emit "-t 0" or negative.
        LiveRunInput input = new(
            InputPath: "/media/in.mkv",
            OutputDirectory: "/tmp/live",
            StartPosition: TimeSpan.FromSeconds(seconds: 60),
            Quality: MakeQuality(),
            SegmentDurationSeconds: 6,
            StopPosition: TimeSpan.FromSeconds(seconds: 50)
        );

        string[] args = LiveFfmpegArgumentBuilder.Build(input: input);

        args.Should().NotContain(unexpected: "-t");
    }
}
