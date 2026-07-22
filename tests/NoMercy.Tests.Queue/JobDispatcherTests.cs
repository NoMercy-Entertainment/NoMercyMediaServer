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

using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Tests.Queue.TestHelpers;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using Xunit;
using IJobDispatcher = NoMercyQueue.Core.Interfaces.IJobDispatcher;

namespace NoMercy.Tests.Queue;

public class JobDispatcherTests
{
    private static (JobDispatcher dispatcher, TestQueueContextAdapter adapter) CreateDispatcher()
    {
        TestQueueContextAdapter adapter = new();
        JobQueue queue = new(context: adapter);
        JobDispatcher dispatcher = new(queue: queue, logger: NullLogger<JobDispatcher>.Instance);
        return (dispatcher, adapter);
    }

    /// <summary>
    /// Dispatch() must never let an enqueue failure escape to the caller — a
    /// dispatcher throwing would take down the code path that called it (e.g.
    /// mid-request in an API handler). It logs and swallows instead.
    /// </summary>
    [Fact]
    public void Dispatch_QueueEnqueueThrows_DoesNotPropagate_JobDispatcherSurvives()
    {
        Mock<IQueueContext> context = new();
        context.Setup(expression: c => c.JobExists(It.IsAny<string>())).Throws<InvalidOperationException>();
        JobQueue queue = new(context: context.Object);
        JobDispatcher dispatcher = new(queue: queue, logger: NullLogger<JobDispatcher>.Instance);
        TestJob job = new() { Message = "will not enqueue" };

        Action act = () => dispatcher.Dispatch(job: job);

        act.Should().NotThrow();
        context.Verify(expression: c => c.AddJob(It.IsAny<QueueJobModel>()), times: Times.Never);
    }

    [Fact]
    public void DispatchChild_QueueEnqueueThrows_DoesNotPropagate()
    {
        Mock<IQueueContext> context = new();
        context.Setup(expression: c => c.JobExists(It.IsAny<string>())).Throws<InvalidOperationException>();
        JobQueue queue = new(context: context.Object);
        JobDispatcher dispatcher = new(queue: queue, logger: NullLogger<JobDispatcher>.Instance);
        TestJob job = new() { Message = "child will not enqueue" };

        Action act = () =>
            dispatcher.DispatchChild(job: job, onQueue: "encoder-child", priority: 1, parentJobId: 5, groupTag: "g");

        act.Should().NotThrow();
        context.Verify(expression: c => c.AddJob(It.IsAny<QueueJobModel>()), times: Times.Never);
    }

    [Fact]
    public void Dispatch_ValidJob_SerializesCorrectly()
    {
        // Arrange
        TestJob testJob = new()
        {
            Message = "Test dispatch",
            HasExecuted = false,
            ShouldFail = false,
        };

        // Act
        string serialized = SerializationHelper.Serialize(obj: testJob);
        object deserialized = SerializationHelper.Deserialize<object>(data: serialized);

        // Assert
        Assert.NotNull(@object: deserialized);
        Assert.IsType<TestJob>(@object: deserialized);

        TestJob deserializedJob = (TestJob)deserialized;
        Assert.Equal(expected: testJob.Message, actual: deserializedJob.Message);
        Assert.Equal(expected: testJob.HasExecuted, actual: deserializedJob.HasExecuted);
        Assert.Equal(expected: testJob.ShouldFail, actual: deserializedJob.ShouldFail);
    }

    [Fact]
    public void Dispatch_ComplexJob_SerializesAndDeserializesCorrectly()
    {
        // Arrange
        AnotherTestJob testJob = new() { Value = 100, HasExecuted = true };

        // Act
        string serialized = SerializationHelper.Serialize(obj: testJob);
        object deserialized = SerializationHelper.Deserialize<object>(data: serialized);

        // Assert
        Assert.NotNull(@object: deserialized);
        Assert.IsType<AnotherTestJob>(@object: deserialized);

        AnotherTestJob deserializedJob = (AnotherTestJob)deserialized;
        Assert.Equal(expected: testJob.Value, actual: deserializedJob.Value);
        Assert.Equal(expected: testJob.HasExecuted, actual: deserializedJob.HasExecuted);
    }

    [Fact]
    public void Dispatch_JobImplementsIShouldQueue_CanBeExecuted()
    {
        // Arrange
        TestJob testJob = new() { Message = "Execution test", HasExecuted = false };

        // Act
        string serialized = SerializationHelper.Serialize(obj: testJob);
        object deserialized = SerializationHelper.Deserialize<object>(data: serialized);

        // Assert
        Assert.IsAssignableFrom<IShouldQueue>(@object: deserialized);

        IShouldQueue executableJob = (IShouldQueue)deserialized;
        Assert.NotNull(@object: executableJob);

        MethodInfo? handleMethod = executableJob.GetType().GetMethod(name: "Handle");
        Assert.NotNull(@object: handleMethod);
    }

    [Fact]
    public void Dispatch_EnqueuesJobWithCorrectQueueAndPriority()
    {
        // Arrange
        (JobDispatcher dispatcher, TestQueueContextAdapter adapter) = CreateDispatcher();
        TestJob testJob = new() { Message = "test enqueue" };

        // Act
        dispatcher.Dispatch(job: testJob);

        // Assert
        Assert.Single(collection: adapter.Jobs);
        QueueJobModel enqueued = adapter.Jobs[index: 0];
        Assert.Equal(expected: "default", actual: enqueued.Queue);
        Assert.Equal(expected: 0, actual: enqueued.Priority);
        Assert.Contains(expectedSubstring: "test enqueue", actualString: enqueued.Payload);
    }

    [Fact]
    public void Dispatch_WithExplicitQueueAndPriority_OverridesJobDefaults()
    {
        // Arrange
        (JobDispatcher dispatcher, TestQueueContextAdapter adapter) = CreateDispatcher();
        TestJob testJob = new() { Message = "override test" };

        // Act
        dispatcher.Dispatch(job: testJob, onQueue: "custom-queue", priority: 99);

        // Assert
        Assert.Single(collection: adapter.Jobs);
        QueueJobModel enqueued = adapter.Jobs[index: 0];
        Assert.Equal(expected: "custom-queue", actual: enqueued.Queue);
        Assert.Equal(expected: 99, actual: enqueued.Priority);
    }

    [Fact]
    public void Dispatch_UsesJobQueueNameAndPriority()
    {
        // Arrange
        (JobDispatcher dispatcher, TestQueueContextAdapter adapter) = CreateDispatcher();
        PriorityTestJob job = new();

        // Act
        dispatcher.Dispatch(job: job);

        // Assert
        Assert.Single(collection: adapter.Jobs);
        QueueJobModel enqueued = adapter.Jobs[index: 0];
        Assert.Equal(expected: "high-priority", actual: enqueued.Queue);
        Assert.Equal(expected: 42, actual: enqueued.Priority);
    }

    [Fact]
    public void Dispatch_DeserializedPayloadMatchesOriginalJob()
    {
        // Arrange
        (JobDispatcher dispatcher, TestQueueContextAdapter adapter) = CreateDispatcher();
        TestJob testJob = new()
        {
            Message = "roundtrip test",
            HasExecuted = false,
            ShouldFail = true,
            ExecutionDelay = 500,
        };

        // Act
        dispatcher.Dispatch(job: testJob);

        // Assert
        Assert.Single(collection: adapter.Jobs);
        object deserialized = SerializationHelper.Deserialize<object>(data: adapter.Jobs[index: 0].Payload);
        Assert.IsType<TestJob>(@object: deserialized);
        TestJob roundtripped = (TestJob)deserialized;
        Assert.Equal(expected: "roundtrip test", actual: roundtripped.Message);
        Assert.False(condition: roundtripped.HasExecuted);
        Assert.True(condition: roundtripped.ShouldFail);
        Assert.Equal(expected: 500, actual: roundtripped.ExecutionDelay);
    }

    [Fact]
    public void Dispatch_MultipleJobs_AllEnqueued()
    {
        // Arrange
        (JobDispatcher dispatcher, TestQueueContextAdapter adapter) = CreateDispatcher();

        // Act
        dispatcher.Dispatch(job: new TestJob { Message = "job1" });
        dispatcher.Dispatch(job: new TestJob { Message = "job2" });
        dispatcher.Dispatch(job: new AnotherTestJob { Value = 10 });

        // Assert - Only 2 unique jobs enqueued because duplicate check prevents job1 and job2 with same type
        // Actually all 3 have different payloads so all should be enqueued
        Assert.Equal(expected: 3, actual: adapter.Jobs.Count);
    }

    [Fact]
    public void Dispatch_DuplicateJob_NotEnqueued()
    {
        // Arrange
        (JobDispatcher dispatcher, TestQueueContextAdapter adapter) = CreateDispatcher();
        TestJob testJob = new() { Message = "duplicate" };

        // Act
        dispatcher.Dispatch(job: testJob);
        dispatcher.Dispatch(job: testJob);

        // Assert - JobQueue deduplicates by payload
        Assert.Single(collection: adapter.Jobs);
    }

    [Fact]
    public void JobDispatcher_ImplementsIJobDispatcher()
    {
        // Arrange & Act
        (JobDispatcher dispatcher, _) = CreateDispatcher();

        // Assert
        Assert.IsAssignableFrom<IJobDispatcher>(@object: dispatcher);
    }

    [Fact]
    public void DispatchChild_EnqueuesChildWithParentLinkageAndGroupTag()
    {
        (JobDispatcher dispatcher, TestQueueContextAdapter adapter) = CreateDispatcher();
        TestJob testJob = new() { Message = "child work" };

        dispatcher.DispatchChild(
            job: testJob,
            onQueue: "encoder-child",
            priority: 7,
            parentJobId: 42,
            groupTag: "group-abc"
        );

        Assert.Single(collection: adapter.Jobs);
        QueueJobModel child = adapter.Jobs[index: 0];
        Assert.Equal(expected: "encoder-child", actual: child.Queue);
        Assert.Equal(expected: 7, actual: child.Priority);
        Assert.Equal(expected: 42, actual: child.ParentJobId!.Value);
        Assert.Equal(expected: "group-abc", actual: child.GroupTag);
        Assert.Contains(expectedSubstring: "child work", actualString: child.Payload);
    }
}

public class PriorityTestJob : IShouldQueue
{
    public string QueueName => "high-priority";
    public int Priority => 42;

    public Task Handle()
    {
        return Task.CompletedTask;
    }
}
