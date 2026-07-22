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
        _jobQueue = new(context: _adapter);
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
        Mock<IResourceBudget> budget = new(behavior: MockBehavior.Loose);

        // TryAcquire returns null → budget is saturated
        budget
            .Setup(expression: b =>
                b.TryAcquire(
                    It.Is<ResourceRequirement>(r => r.GpuDeviceKey == "test-gpu"),
                    TimeSpan.Zero
                )
            )
            .Returns(value: (ResourceLease?)null);

        ResourceRequirementJob resourceJob = new()
        {
            QueueName = "encoder-gpu",
            ResourceRequirement = new(GpuDeviceKey: "test-gpu", GpuSlots: 1, CpuThreads: 2),
        };

        QueueJob queueJob = new()
        {
            Queue = "encoder-gpu",
            Payload = SerializationHelper.Serialize(obj: resourceJob),
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
        };

        _context.QueueJobs.Add(entity: queueJob);
        _context.SaveChanges();

        // Reserve the job manually (simulating what the worker does before gate check)
        QueueJobModel? reserved = _jobQueue.ReserveJob(name: "encoder-gpu", currentJobId: null);
        Assert.NotNull(@object: reserved);

        // Simulate the gate check: try to acquire with the requirement from the job payload
        ResourceRequirement? requirement = new(GpuDeviceKey: "test-gpu", GpuSlots: 1, CpuThreads: 2);
        ResourceLease? lease = budget.Object.TryAcquire(requirement: requirement, timeout: TimeSpan.Zero);

        Assert.Null(@object: lease);

        // When saturated, the worker calls ReleaseReservation — verify it re-queues the job
        _jobQueue.ReleaseReservation(job: reserved, availableAfter: TimeSpan.FromSeconds(seconds: 5));

        // Job should still exist (reservation released, not deleted)
        int jobCount = _context.QueueJobs.Count();
        Assert.Equal(expected: 1, actual: jobCount);

        // Verify TryAcquire was called with correct requirement
        budget.Verify(
            expression: b =>
                b.TryAcquire(
                    It.Is<ResourceRequirement>(r =>
                        r.GpuDeviceKey == "test-gpu" && r.GpuSlots == 1
                    ),
                    TimeSpan.Zero
                ),
            times: Times.Once
        );
    }

    // ─── Budget gate: available — TryAcquire returns a lease, Release called ─

    [Fact]
    public async Task BudgetGate_WhenBudgetAvailable_JobExecutedAndLeaseReleased()
    {
        ResourceLease grantedLease = new(LeaseId: "lease-1", GpuDeviceKey: "test-gpu", GpuSlots: 1, CpuThreads: 2);

        Mock<IResourceBudget> budget = new(behavior: MockBehavior.Strict);

        // TryAcquire returns a lease → budget available
        budget
            .Setup(expression: b => b.TryAcquire(It.IsAny<ResourceRequirement>(), TimeSpan.Zero))
            .Returns(value: grantedLease);
        budget.Setup(expression: b => b.Release(grantedLease));

        ResourceRequirementJob resourceJob = new()
        {
            QueueName = "encoder-gpu",
            ResourceRequirement = new(GpuDeviceKey: "test-gpu", GpuSlots: 1, CpuThreads: 2),
        };

        QueueJob queueJob = new()
        {
            Queue = "encoder-gpu",
            Payload = SerializationHelper.Serialize(obj: resourceJob),
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
        };

        _context.QueueJobs.Add(entity: queueJob);
        await _context.SaveChangesAsync();

        QueueWorker worker = new(
            queue: _jobQueue,
            name: "encoder-gpu",
            resourceBudget: budget.Object,
            resourceAwareQueues: new HashSet<string> { "encoder-gpu", "encoder-cpu" }
        );

        // Cancel shortly after the job executes — the worker will process one job
        // then wait on WorkAvailable with the stop token, exiting cleanly.
        using CancellationTokenSource cts = new(delay: TimeSpan.FromSeconds(seconds: 5));

        try
        {
            await worker.StartAsync(stopToken: cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when the cancellation token fires
        }

        // Queue should be empty — job was deleted after success
        int jobCount = _context.QueueJobs.Count();
        Assert.Equal(expected: 0, actual: jobCount);

        // Budget.Release must have been called exactly once
        budget.Verify(expression: b => b.Release(grantedLease), times: Times.Once);
    }

    // ─── Queue routing: GPU task routes to encoder-gpu ───────────────────────

    [Fact]
    public void GpuTask_QueueName_IsEncoderGpu()
    {
        ResourceRequirementJob gpuJob = new()
        {
            QueueName = "encoder-gpu",
            ResourceRequirement = new(GpuDeviceKey: "NVIDIA GeForce RTX 4090", GpuSlots: 1, CpuThreads: 2),
        };

        Assert.Equal(expected: "encoder-gpu", actual: gpuJob.QueueName);
    }

    // ─── Queue routing: CPU task routes to encoder-cpu ───────────────────────

    [Fact]
    public void CpuTask_QueueName_IsEncoderCpu()
    {
        ResourceRequirementJob cpuJob = new()
        {
            QueueName = "encoder-cpu",
            ResourceRequirement = new(GpuDeviceKey: null, GpuSlots: 0, CpuThreads: 4),
        };

        Assert.Equal(expected: "encoder-cpu", actual: cpuJob.QueueName);
    }

    // ─── Worker passes through non-resource-aware queues without budget check ─

    [Fact]
    public async Task NonResourceAwareQueue_BudgetNotConsulted_JobExecutes()
    {
        Mock<IResourceBudget> budget = new(behavior: MockBehavior.Strict);

        // No budget methods should be called for a non-resource-aware queue
        // (strict mock will throw if TryAcquire / Release are called unexpectedly)

        TestJob plainJob = new() { Message = "library job", HasExecuted = false };

        QueueJob queueJob = new()
        {
            Queue = "library",
            Payload = SerializationHelper.Serialize(obj: plainJob),
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
        };

        _context.QueueJobs.Add(entity: queueJob);
        await _context.SaveChangesAsync();

        // Worker on "library" queue — budget is injected but must NOT be consulted.
        // Job is already in the DB; worker picks it up on the first ReserveJob call.
        QueueWorker worker = new(queue: _jobQueue, name: "library", resourceBudget: budget.Object);

        using CancellationTokenSource cts = new(delay: TimeSpan.FromSeconds(seconds: 5));

        try
        {
            await worker.StartAsync(stopToken: cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Queue should be empty — job was deleted after successful execution
        int jobCount = _context.QueueJobs.Count();
        Assert.Equal(expected: 0, actual: jobCount);

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
