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
using NoMercy.Resources;
using NoMercyQueue.Core;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Resources;

namespace NoMercy.Tests.MediaProcessing.Jobs;

// Regression net for the field bug "encoder-gpu queue permanently jammed":
// EncodeTaskJob.QueueName is computed straight off Task.Resources.GpuDeviceKey,
// so once DegradeToSoftware clears the GPU pin the SAME job instance must
// route to encoder-cpu on its next dispatch. QueueWorker's budget gate calls
// this when the requirement's GpuDeviceKey is a device that will never be
// registered on this host (e.g. h264_amf on an NVIDIA-only box).
public class EncodeTaskJobDegradeTests
{
    private static EncodeTaskJob BuildJob(ResourceRequirement? resources) =>
        new()
        {
            Id = "42",
            FolderId = Ulid.NewUlid(),
            LibraryId = Ulid.NewUlid(),
            InputFile = "/movies/test/test.mkv",
            PresetId = Ulid.NewUlid(),
            Task = new DecomposedTask(
                TaskId: "task-1",
                ParentJobId: 1,
                GroupTag: "group-1",
                Kind: EncodeTaskKind.Video,
                OutputIndex: 0,
                Resources: resources
            ),
        };

    [Fact]
    public void DegradeToSoftware_GpuPinnedTask_DropsGpuKeyAndReroutesToEncoderCpu()
    {
        EncodeTaskJob job = BuildJob(
            new ResourceRequirement(GpuDeviceKey: "h264_amf", GpuSlots: 1, CpuThreads: 2)
        );

        Assert.Equal(QueueNames.EncoderGpu, job.QueueName);

        IShouldQueue? degraded = job.DegradeToSoftware();

        Assert.NotNull(degraded);
        Assert.Same(job, degraded);
        Assert.Null(job.Task.Resources!.GpuDeviceKey);
        Assert.Equal(0, job.Task.Resources.GpuSlots);
        Assert.Equal(QueueNames.EncoderCpu, job.QueueName);
        Assert.Equal(QueueNames.EncoderCpu, degraded.QueueName);
    }

    // The degraded task no longer has a GPU to encode on — it becomes a full
    // software encode. Carrying the hardware task's small CPU reservation over
    // told the budget this was a cheap run and let a second encode start
    // beside it, which is precisely the host-pegging the budget exists to stop.
    [Fact]
    public void DegradeToSoftware_RaisesCpuReservationToASoftwareEncodeShare()
    {
        EncodeTaskJob job = BuildJob(
            new ResourceRequirement(GpuDeviceKey: "h264_amf", GpuSlots: 1, CpuThreads: 2)
        );

        job.DegradeToSoftware();

        Assert.Equal(EncodeThreadBudget.SoftwareEncode, job.Task.Resources!.CpuThreads);
    }

    [Fact]
    public void DegradeToSoftware_NeverLowersAnAlreadyLargerReservation()
    {
        // A CPU-tonemap hardware task already reserves the software share;
        // degrading it must not shrink the reservation.
        int oversized = EncodeThreadBudget.SoftwareEncode + 4;
        EncodeTaskJob job = BuildJob(
            new ResourceRequirement(GpuDeviceKey: "h264_amf", GpuSlots: 1, CpuThreads: oversized)
        );

        job.DegradeToSoftware();

        Assert.Equal(oversized, job.Task.Resources!.CpuThreads);
    }

    [Fact]
    public void DegradeToSoftware_AlreadyCpuOnlyTask_ReturnsNull()
    {
        EncodeTaskJob job = BuildJob(
            new ResourceRequirement(GpuDeviceKey: null, GpuSlots: 0, CpuThreads: 4)
        );

        IShouldQueue? degraded = job.DegradeToSoftware();

        Assert.Null(degraded);
        Assert.Equal(QueueNames.EncoderCpu, job.QueueName);
    }

    [Fact]
    public void DegradeToSoftware_NoResourceRequirementAtAll_ReturnsNull()
    {
        EncodeTaskJob job = BuildJob(resources: null);

        IShouldQueue? degraded = job.DegradeToSoftware();

        Assert.Null(degraded);
    }
}
