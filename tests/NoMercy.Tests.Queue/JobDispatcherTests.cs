using System.Reflection;
using NoMercy.Queue;
using NoMercy.Queue.Core.Models;
using NoMercy.Tests.Queue.TestHelpers;
using Xunit;
using IJobDispatcher = NoMercy.Queue.Core.Interfaces.IJobDispatcher;

namespace NoMercy.Tests.Queue;

public class JobDispatcherTests
{
    private static (JobDispatcher dispatcher, TestQueueContextAdapter adapter) CreateDispatcher()
    {
        TestQueueContextAdapter adapter = new();
        JobQueue queue = new(adapter);
        JobDispatcher dispatcher = new(queue);
        return (dispatcher, adapter);
    }

    [Fact]
    public void Dispatch_ValidJob_SerializesCorrectly()
    {
        // Arrange
        TestJob testJob = new()
        {
            Message = "Test dispatch",
            HasExecuted = false,
            ShouldFail = false
        };

        // Act
        string serialized = SerializationHelper.Serialize(testJob);
        object deserialized = SerializationHelper.Deserialize<object>(serialized);

        // Assert
        Assert.NotNull(deserialized);
        Assert.IsType<TestJob>(deserialized);

        TestJob deserializedJob = (TestJob)deserialized;
        Assert.Equal(testJob.Message, deserializedJob.Message);
        Assert.Equal(testJob.HasExecuted, deserializedJob.HasExecuted);
        Assert.Equal(testJob.ShouldFail, deserializedJob.ShouldFail);
    }

    [Fact]
    public void Dispatch_ComplexJob_SerializesAndDeserializesCorrectly()
    {
        // Arrange
        AnotherTestJob testJob = new()
        {
            Value = 100,
            HasExecuted = true
        };

        // Act
        string serialized = SerializationHelper.Serialize(testJob);
        object deserialized = SerializationHelper.Deserialize<object>(serialized);

        // Assert
        Assert.NotNull(deserialized);
        Assert.IsType<AnotherTestJob>(deserialized);

        AnotherTestJob deserializedJob = (AnotherTestJob)deserialized;
        Assert.Equal(testJob.Value, deserializedJob.Value);
        Assert.Equal(testJob.HasExecuted, deserializedJob.HasExecuted);
    }

    [Fact]
    public void Dispatch_JobImplementsIShouldQueue_CanBeExecuted()
    {
        // Arrange
        TestJob testJob = new()
        {
            Message = "Execution test",
            HasExecuted = false
        };

        // Act
        string serialized = SerializationHelper.Serialize(testJob);
        object deserialized = SerializationHelper.Deserialize<object>(serialized);

        // Assert
        Assert.IsAssignableFrom<IShouldQueue>(deserialized);

        IShouldQueue executableJob = (IShouldQueue)deserialized;
        Assert.NotNull(executableJob);

        MethodInfo? handleMethod = executableJob.GetType().GetMethod("Handle");
        Assert.NotNull(handleMethod);
    }

    [Fact]
    public void Dispatch_EnqueuesJobWithCorrectQueueAndPriority()
    {
        // Arrange
        (JobDispatcher dispatcher, TestQueueContextAdapter adapter) = CreateDispatcher();
        TestJob testJob = new() { Message = "test enqueue" };

        // Act
        dispatcher.Dispatch(testJob);

        // Assert
        Assert.Single(adapter.Jobs);
        QueueJobModel enqueued = adapter.Jobs[0];
        Assert.Equal("default", enqueued.Queue);
        Assert.Equal(0, enqueued.Priority);
        Assert.Contains("test enqueue", enqueued.Payload);
    }

    [Fact]
    public void Dispatch_WithExplicitQueueAndPriority_OverridesJobDefaults()
    {
        // Arrange
        (JobDispatcher dispatcher, TestQueueContextAdapter adapter) = CreateDispatcher();
        TestJob testJob = new() { Message = "override test" };

        // Act
        dispatcher.Dispatch(testJob, "custom-queue", 99);

        // Assert
        Assert.Single(adapter.Jobs);
        QueueJobModel enqueued = adapter.Jobs[0];
        Assert.Equal("custom-queue", enqueued.Queue);
        Assert.Equal(99, enqueued.Priority);
    }

    [Fact]
    public void Dispatch_UsesJobQueueNameAndPriority()
    {
        // Arrange
        (JobDispatcher dispatcher, TestQueueContextAdapter adapter) = CreateDispatcher();
        PriorityTestJob job = new();

        // Act
        dispatcher.Dispatch(job);

        // Assert
        Assert.Single(adapter.Jobs);
        QueueJobModel enqueued = adapter.Jobs[0];
        Assert.Equal("high-priority", enqueued.Queue);
        Assert.Equal(42, enqueued.Priority);
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
            ExecutionDelay = 500
        };

        // Act
        dispatcher.Dispatch(testJob);

        // Assert
        Assert.Single(adapter.Jobs);
        object deserialized = SerializationHelper.Deserialize<object>(adapter.Jobs[0].Payload);
        Assert.IsType<TestJob>(deserialized);
        TestJob roundtripped = (TestJob)deserialized;
        Assert.Equal("roundtrip test", roundtripped.Message);
        Assert.False(roundtripped.HasExecuted);
        Assert.True(roundtripped.ShouldFail);
        Assert.Equal(500, roundtripped.ExecutionDelay);
    }

    [Fact]
    public void Dispatch_MultipleJobs_AllEnqueued()
    {
        // Arrange
        (JobDispatcher dispatcher, TestQueueContextAdapter adapter) = CreateDispatcher();

        // Act
        dispatcher.Dispatch(new TestJob { Message = "job1" });
        dispatcher.Dispatch(new TestJob { Message = "job2" });
        dispatcher.Dispatch(new AnotherTestJob { Value = 10 });

        // Assert - Only 2 unique jobs enqueued because duplicate check prevents job1 and job2 with same type
        // Actually all 3 have different payloads so all should be enqueued
        Assert.Equal(3, adapter.Jobs.Count);
    }

    [Fact]
    public void Dispatch_DuplicateJob_NotEnqueued()
    {
        // Arrange
        (JobDispatcher dispatcher, TestQueueContextAdapter adapter) = CreateDispatcher();
        TestJob testJob = new() { Message = "duplicate" };

        // Act
        dispatcher.Dispatch(testJob);
        dispatcher.Dispatch(testJob);

        // Assert - JobQueue deduplicates by payload
        Assert.Single(adapter.Jobs);
    }

    [Fact]
    public void JobDispatcher_ImplementsIJobDispatcher()
    {
        // Arrange & Act
        (JobDispatcher dispatcher, _) = CreateDispatcher();

        // Assert
        Assert.IsAssignableFrom<IJobDispatcher>(dispatcher);
    }
}

public class PriorityTestJob : NoMercy.Queue.IShouldQueue
{
    public string QueueName => "high-priority";
    public int Priority => 42;

    public Task Handle()
    {
        return Task.CompletedTask;
    }
}
