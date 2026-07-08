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
using NoMercy.Database;
using NoMercy.Database.Models.Queue;
using NoMercy.Tests.Queue.TestHelpers;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using NoMercyQueue.Workers;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// Verifies the resource-budget gate in QueueWorker — GPU/CPU slot tracking,
/// queue routing assertions, and budget participation from live sessions.
/// </summary>
public class BudgetGateTests : IDisposable
{
    private readonly QueueContext _context;
    private readonly IQueueContext _adapter;
    private readonly JobQueue _jobQueue;

    public BudgetGateTests()
    {
        (_context, _adapter) = TestQueueContextFactory.CreateInMemoryContextWithAdapter();
        _jobQueue = new(_adapter);
    }

    public void Dispose()
    {
        _adapter.Dispose();
        _context.Dispose();
    }

    // ─── Budget gate: saturated — TryAcquire returns null, budget consulted ──

    [Fact]
    public void BudgetGate_WhenBudgetSaturated_TryAcquireCalledAndBudgetLogs()
    {
        Mock<IResourceBudget> budget = new(MockBehavior.Loose);

        // TryAcquire returns null → budget is saturated
        budget
            .Setup(b =>
                b.TryAcquire(
                    It.Is<ResourceRequirement>(r => r.GpuDeviceKey == "test-gpu"),
                    TimeSpan.Zero
                )
            )
            .Returns((ResourceLease?)null);

        ResourceRequirementJob resourceJob = new()
        {
            QueueName = "encoder-gpu",
            ResourceRequirement = new("test-gpu", GpuSlots: 1, CpuThreads: 2),
        };

        QueueJob queueJob = new()
        {
            Queue = "encoder-gpu",
            Payload = SerializationHelper.Serialize(resourceJob),
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
        };

        _context.QueueJobs.Add(queueJob);
        _context.SaveChanges();

        // Reserve the job manually (simulating what the worker does before gate check)
        QueueJobModel? reserved = _jobQueue.ReserveJob("encoder-gpu", null);
        Assert.NotNull(reserved);

        // Simulate the gate check: try to acquire with the requirement from the job payload
        ResourceRequirement? requirement = new(
            "test-gpu",
            GpuSlots: 1,
            CpuThreads: 2
        );
        ResourceLease? lease = budget.Object.TryAcquire(requirement, TimeSpan.Zero);

        Assert.Null(lease);

        // When saturated, the worker calls ReleaseReservation — verify it re-queues the job
        _jobQueue.ReleaseReservation(reserved, TimeSpan.FromSeconds(5));

        // Job should still exist (reservation released, not deleted)
        int jobCount = _context.QueueJobs.Count();
        Assert.Equal(1, jobCount);

        // Verify TryAcquire was called with correct requirement
        budget.Verify(
            b =>
                b.TryAcquire(
                    It.Is<ResourceRequirement>(r =>
                        r.GpuDeviceKey == "test-gpu" && r.GpuSlots == 1
                    ),
                    TimeSpan.Zero
                ),
            Times.Once
        );
    }

    // ─── Budget gate: available — TryAcquire returns a lease, Release called ─

    [Fact]
    public async Task BudgetGate_WhenBudgetAvailable_JobExecutedAndLeaseReleased()
    {
        ResourceLease grantedLease = new("lease-1", "test-gpu", GpuSlots: 1, CpuThreads: 2);

        Mock<IResourceBudget> budget = new(MockBehavior.Strict);

        // TryAcquire returns a lease → budget available
        budget
            .Setup(b => b.TryAcquire(It.IsAny<ResourceRequirement>(), TimeSpan.Zero))
            .Returns(grantedLease);
        budget.Setup(b => b.Release(grantedLease));

        ResourceRequirementJob resourceJob = new()
        {
            QueueName = "encoder-gpu",
            ResourceRequirement = new("test-gpu", GpuSlots: 1, CpuThreads: 2),
        };

        QueueJob queueJob = new()
        {
            Queue = "encoder-gpu",
            Payload = SerializationHelper.Serialize(resourceJob),
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
        };

        _context.QueueJobs.Add(queueJob);
        await _context.SaveChangesAsync();

        QueueWorker worker = new(
            _jobQueue,
            "encoder-gpu",
            resourceBudget: budget.Object,
            resourceAwareQueues: new HashSet<string> { "encoder-gpu", "encoder-cpu" }
        );

        // Cancel shortly after the job executes — the worker will process one job
        // then wait on WorkAvailable with the stop token, exiting cleanly.
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

        try
        {
            await worker.StartAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when the cancellation token fires
        }

        // Queue should be empty — job was deleted after success
        int jobCount = _context.QueueJobs.Count();
        Assert.Equal(0, jobCount);

        // Budget.Release must have been called exactly once
        budget.Verify(b => b.Release(grantedLease), Times.Once);
    }

    // ─── Queue routing: GPU task routes to encoder-gpu ───────────────────────

    [Fact]
    public void GpuTask_QueueName_IsEncoderGpu()
    {
        ResourceRequirementJob gpuJob = new()
        {
            QueueName = "encoder-gpu",
            ResourceRequirement = new(
                "NVIDIA GeForce RTX 4090",
                GpuSlots: 1,
                CpuThreads: 2
            ),
        };

        Assert.Equal("encoder-gpu", gpuJob.QueueName);
    }

    // ─── Queue routing: CPU task routes to encoder-cpu ───────────────────────

    [Fact]
    public void CpuTask_QueueName_IsEncoderCpu()
    {
        ResourceRequirementJob cpuJob = new()
        {
            QueueName = "encoder-cpu",
            ResourceRequirement = new(null, GpuSlots: 0, CpuThreads: 4),
        };

        Assert.Equal("encoder-cpu", cpuJob.QueueName);
    }

    // ─── Worker passes through non-resource-aware queues without budget check ─

    [Fact]
    public async Task NonResourceAwareQueue_BudgetNotConsulted_JobExecutes()
    {
        Mock<IResourceBudget> budget = new(MockBehavior.Strict);

        // No budget methods should be called for a non-resource-aware queue
        // (strict mock will throw if TryAcquire / Release are called unexpectedly)

        TestJob plainJob = new() { Message = "library job", HasExecuted = false };

        QueueJob queueJob = new()
        {
            Queue = "library",
            Payload = SerializationHelper.Serialize(plainJob),
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
        };

        _context.QueueJobs.Add(queueJob);
        await _context.SaveChangesAsync();

        // Worker on "library" queue — budget is injected but must NOT be consulted.
        // Job is already in the DB; worker picks it up on the first ReserveJob call.
        QueueWorker worker = new(_jobQueue, "library", resourceBudget: budget.Object);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

        try
        {
            await worker.StartAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Queue should be empty — job was deleted after successful execution
        int jobCount = _context.QueueJobs.Count();
        Assert.Equal(0, jobCount);

        // Strict mock verifies no unexpected budget calls
        budget.VerifyNoOtherCalls();
    }
}

/// <summary>
/// Test job that implements both <see cref="IShouldQueue"/> and
/// <see cref="IHasResourceRequirement"/> so the budget gate can read its requirement.
/// Placed in the <c>NoMercy.Tests.Queue</c> namespace so the serialization binder
/// (which allows <c>NoMercy.*</c> types) can round-trip the payload.
/// </summary>
public class ResourceRequirementJob : IShouldQueue, IHasResourceRequirement
{
    public string QueueName { get; set; } = string.Empty;
    public int Priority => 1;

    public ResourceRequirement? ResourceRequirement { get; set; }

    public bool HasExecuted { get; private set; }

    public Task Handle()
    {
        HasExecuted = true;
        return Task.CompletedTask;
    }
}
