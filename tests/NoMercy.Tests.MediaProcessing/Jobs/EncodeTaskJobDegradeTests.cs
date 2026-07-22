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
            resources: new ResourceRequirement(GpuDeviceKey: "h264_amf", GpuSlots: 1, CpuThreads: 2)
        );

        Assert.Equal(expected: QueueNames.EncoderGpu, actual: job.QueueName);

        IShouldQueue? degraded = job.DegradeToSoftware();

        Assert.NotNull(@object: degraded);
        Assert.Same(expected: job, actual: degraded);
        Assert.Null(@object: job.Task.Resources!.GpuDeviceKey);
        Assert.Equal(expected: 0, actual: job.Task.Resources.GpuSlots);
        Assert.Equal(expected: 2, actual: job.Task.Resources.CpuThreads);
        Assert.Equal(expected: QueueNames.EncoderCpu, actual: job.QueueName);
        Assert.Equal(expected: QueueNames.EncoderCpu, actual: degraded.QueueName);
    }

    [Fact]
    public void DegradeToSoftware_AlreadyCpuOnlyTask_ReturnsNull()
    {
        EncodeTaskJob job = BuildJob(
            resources: new ResourceRequirement(GpuDeviceKey: null, GpuSlots: 0, CpuThreads: 4)
        );

        IShouldQueue? degraded = job.DegradeToSoftware();

        Assert.Null(@object: degraded);
        Assert.Equal(expected: QueueNames.EncoderCpu, actual: job.QueueName);
    }

    [Fact]
    public void DegradeToSoftware_NoResourceRequirementAtAll_ReturnsNull()
    {
        EncodeTaskJob job = BuildJob(resources: null);

        IShouldQueue? degraded = job.DegradeToSoftware();

        Assert.Null(@object: degraded);
    }
}
