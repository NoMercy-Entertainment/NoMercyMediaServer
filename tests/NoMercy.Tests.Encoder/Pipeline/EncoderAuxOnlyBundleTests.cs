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

using NoMercy.Encoder.Decomposition;

namespace NoMercy.Tests.Encoder.Pipeline;

/// <summary>
/// A bundled Whole task that covers no video and no audio writes no variant
/// playlists. Finalizing it as a full encode measures the whole plan's variants
/// against a staging directory that never received them and fails the task on
/// "Master playlist would list zero variants" — which costs the job its
/// post-encode phase, and with it the subtitle OCR that runs there.
/// </summary>
public class EncoderAuxOnlyBundleTests
{
    private static DecomposedTask Task(
        EncodeTaskKind kind,
        int[]? video = null,
        int[]? audio = null,
        bool? thumbs = null
    ) =>
        new(
            TaskId: "task-0",
            ParentJobId: 1,
            GroupTag: "group",
            Kind: kind,
            OutputIndex: 0,
            Resources: null,
            VideoSliceIndexes: video,
            AudioSliceIndexes: audio,
            IncludeThumbnails: thumbs
        );

    private static bool Is(DecomposedTask? task) => task?.IsAuxOnlyBundle == true;

    [Fact]
    public void ThumbnailsOnlyBundle_IsAuxOnly()
    {
        // What the coordinator dispatches when only the thumbnail strip is
        // missing: a Whole bundle covering no streams at all.
        Is(Task(EncodeTaskKind.Whole, video: [], audio: [], thumbs: true)).Should().BeTrue();
    }

    [Fact]
    public void FullBundle_IsNotAuxOnly()
    {
        // Null slice indexes mean "every output" — the ordinary whole encode,
        // which must still write its master playlist.
        Is(Task(EncodeTaskKind.Whole)).Should().BeFalse();
    }

    [Fact]
    public void BundleCarryingVideo_IsNotAuxOnly()
    {
        Is(Task(EncodeTaskKind.Whole, video: [0], audio: [])).Should().BeFalse();
    }

    [Fact]
    public void BundleCarryingAudio_IsNotAuxOnly()
    {
        // Audio alone still produces a variant playlist the master must list.
        Is(Task(EncodeTaskKind.Whole, video: [], audio: [0])).Should().BeFalse();
    }

    [Fact]
    public void PerStreamThumbnailTask_IsNotAuxOnly_ItDefersFinalizeAnyway()
    {
        // Only Whole-kind bundles reach the finalize branch this guards; a
        // per-stream task already defers to the coordinator's FinalizeOnly pass,
        // and must not be mistaken for a bundle.
        Is(Task(EncodeTaskKind.Thumbnails, video: [], audio: [])).Should().BeFalse();
    }

    [Fact]
    public void NoTaskFilter_IsNotAuxOnly()
    {
        // A plain undecomposed encode has no filter at all.
        Is(null).Should().BeFalse();
    }
}
