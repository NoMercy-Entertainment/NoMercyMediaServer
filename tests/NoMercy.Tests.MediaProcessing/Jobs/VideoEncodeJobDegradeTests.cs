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
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs.Support;
using NoMercy.Resources;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Resources;

namespace NoMercy.Tests.MediaProcessing.Jobs;

// VideoEncodeJob.QueueName is always "encoder" regardless of GPU/CPU need;
// DegradeToSoftware only clears the GPU pin on the CURRENT bundle's Resources
// so the budget gate re-plans it as a software encode. QueueWorker calls this
// when the requirement's GpuDeviceKey is a device that will never be
// registered on this host (e.g. h264_amf on an NVIDIA-only box). Ported from
// the former EncodeTaskJobDegradeTests when the coordinator/child split
// collapsed into one job type — the bundle now lives on the coordinator's own
// CoordinatorState.Bundles[CurrentBundleIndex] instead of a child's own Task.
public class VideoEncodeJobDegradeTests
{
    private static VideoEncodeJob BuildJob(ResourceRequirement? resources) =>
        new()
        {
            Id = "42",
            FolderId = Ulid.NewUlid(),
            LibraryId = Ulid.NewUlid(),
            InputFile = "/movies/test/test.mkv",
            Coordinator = new(
                GroupTag: "group-1",
                TaskIds: ["task-1"],
                Phase: CoordinatorPhase.WaitChildren,
                Pass1DispatchedAt: null,
                Pass2DispatchedAt: null,
                Pass1StatsPath: null,
                PresetId: Ulid.NewUlid(),
                ExpectedFinalCount: 1,
                Bundles:
                [
                    new DecomposedTask(
                        TaskId: "task-1",
                        ParentJobId: 1,
                        GroupTag: "group-1",
                        Kind: EncodeTaskKind.Video,
                        OutputIndex: 0,
                        Resources: resources
                    ),
                ],
                CurrentBundleIndex: 0
            ),
        };

    [Fact]
    public void DegradeToSoftware_GpuPinnedBundle_DropsGpuKeyAndStaysOnEncoder()
    {
        VideoEncodeJob job = BuildJob(
            new ResourceRequirement(GpuDeviceKey: "h264_amf", GpuSlots: 1, CpuThreads: 2)
        );

        Assert.Equal("encoder", job.QueueName);

        IShouldQueue? degraded = job.DegradeToSoftware();

        Assert.NotNull(degraded);
        Assert.Same(job, degraded);
        Assert.Null(job.Coordinator!.Bundles![0].Resources!.GpuDeviceKey);
        Assert.Equal(0, job.Coordinator.Bundles[0].Resources!.GpuSlots);
        Assert.Equal("encoder", job.QueueName);
        Assert.Equal("encoder", degraded.QueueName);
    }

    // The degraded bundle no longer has a GPU to encode on — it becomes a full
    // software encode. Carrying the hardware bundle's small CPU reservation
    // over told the budget this was a cheap run and let a second encode start
    // beside it, which is precisely the host-pegging the budget exists to stop.
    [Fact]
    public void DegradeToSoftware_RaisesCpuReservationToASoftwareEncodeShare()
    {
        VideoEncodeJob job = BuildJob(
            new ResourceRequirement(GpuDeviceKey: "h264_amf", GpuSlots: 1, CpuThreads: 2)
        );

        job.DegradeToSoftware();

        Assert.Equal(
            EncodeThreadBudget.SoftwareEncode,
            job.Coordinator!.Bundles![0].Resources!.CpuThreads
        );
    }

    [Fact]
    public void DegradeToSoftware_NeverLowersAnAlreadyLargerReservation()
    {
        // A CPU-tonemap hardware bundle already reserves the software share;
        // degrading it must not shrink the reservation.
        int oversized = EncodeThreadBudget.SoftwareEncode + 4;
        VideoEncodeJob job = BuildJob(
            new ResourceRequirement(GpuDeviceKey: "h264_amf", GpuSlots: 1, CpuThreads: oversized)
        );

        job.DegradeToSoftware();

        Assert.Equal(oversized, job.Coordinator!.Bundles![0].Resources!.CpuThreads);
    }

    [Fact]
    public void DegradeToSoftware_AlreadyCpuOnlyBundle_ReturnsNull()
    {
        VideoEncodeJob job = BuildJob(
            new ResourceRequirement(GpuDeviceKey: null, GpuSlots: 0, CpuThreads: 4)
        );

        IShouldQueue? degraded = job.DegradeToSoftware();

        Assert.Null(degraded);
        Assert.Equal("encoder", job.QueueName);
    }

    [Fact]
    public void DegradeToSoftware_NoResourceRequirementAtAll_ReturnsNull()
    {
        VideoEncodeJob job = BuildJob(resources: null);

        IShouldQueue? degraded = job.DegradeToSoftware();

        Assert.Null(degraded);
    }

    [Fact]
    public void DegradeToSoftware_NoCoordinatorYet_ReturnsNull()
    {
        // Still in the ungated decompose phase — nothing to degrade.
        VideoEncodeJob job = new()
        {
            Id = "42",
            FolderId = Ulid.NewUlid(),
            LibraryId = Ulid.NewUlid(),
            InputFile = "/movies/test/test.mkv",
        };

        Assert.Null(job.DegradeToSoftware());
    }
}
