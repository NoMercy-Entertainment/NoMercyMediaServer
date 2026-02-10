using NoMercy.Queue;
using NoMercy.Tests.Queue.TestHelpers;
using Xunit;

namespace NoMercy.Tests.Queue;

public class SerializationHelperTests
{
    [Fact]
    public void Serialize_SimpleJob_ReturnsJsonString()
    {
        // Arrange
        TestJob testJob = new()
        {
            Message = "Test message",
            HasExecuted = false,
            ShouldFail = false
        };

        // Act
        string serialized = SerializationHelper.Serialize(testJob);

        // Assert
        Assert.NotNull(serialized);
        Assert.NotEmpty(serialized);
        Assert.Contains("Test message", serialized);
        Assert.Contains("$type", serialized); // TypeNameHandling.All should include type info
    }

    [Fact]
    public void Deserialize_ValidJsonString_ReturnsCorrectObject()
    {
        // Arrange
        TestJob originalJob = new()
        {
            Message = "Original message",
            HasExecuted = true,
            ShouldFail = false
        };
        string serialized = SerializationHelper.Serialize(originalJob);

        // Act
        TestJob deserialized = SerializationHelper.Deserialize<TestJob>(serialized);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(originalJob.Message, deserialized.Message);
        Assert.Equal(originalJob.HasExecuted, deserialized.HasExecuted);
        Assert.Equal(originalJob.ShouldFail, deserialized.ShouldFail);
    }

    [Fact]
    public void Serialize_Deserialize_ComplexJob_MaintainsIntegrity()
    {
        // Arrange
        AnotherTestJob originalJob = new()
        {
            Value = 42,
            HasExecuted = true
        };

        // Act
        string serialized = SerializationHelper.Serialize(originalJob);
        AnotherTestJob deserialized = SerializationHelper.Deserialize<AnotherTestJob>(serialized);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(originalJob.Value, deserialized.Value);
        Assert.Equal(originalJob.HasExecuted, deserialized.HasExecuted);
    }

    [Fact]
    public void Deserialize_AsObject_ReturnsCorrectType()
    {
        // Arrange
        TestJob originalJob = new()
        {
            Message = "Type test",
            HasExecuted = false
        };
        string serialized = SerializationHelper.Serialize(originalJob);

        // Act
        object deserialized = SerializationHelper.Deserialize<object>(serialized);

        // Assert
        Assert.NotNull(deserialized);
        Assert.IsType<TestJob>(deserialized);
        TestJob testJob = (TestJob)deserialized;
        Assert.Equal(originalJob.Message, testJob.Message);
    }

    [Fact]
    public void Serialize_NullValues_HandlesCorrectly()
    {
        // Arrange
        TestJob testJob = new()
        {
            Message = null!, // Testing null handling
            HasExecuted = false
        };

        // Act
        string serialized = SerializationHelper.Serialize(testJob);
        TestJob deserialized = SerializationHelper.Deserialize<TestJob>(serialized);

        // Assert
        Assert.NotNull(deserialized);
        // JSON.NET with NullValueHandling.Ignore actually omits null properties from serialization
        // but deserializes them as their default values (empty string for string, etc.)
        Assert.True(string.IsNullOrEmpty(deserialized.Message));
        Assert.Equal(testJob.HasExecuted, deserialized.HasExecuted);
    }

    [Fact]
    public void Serialize_CamelCaseNaming_UsesCorrectFormat()
    {
        // Arrange
        TestJob testJob = new()
        {
            Message = "CamelCase test",
            HasExecuted = true
        };

        // Act
        string serialized = SerializationHelper.Serialize(testJob);

        // Assert
        Assert.Contains("hasExecuted", serialized); // Should be camelCase
        Assert.Contains("message", serialized);     // Should be camelCase
    }

    [Fact]
    public void Deserialize_IShouldQueueJob_CanBeCastToInterface()
    {
        // Arrange — serialize a valid job implementing IShouldQueue
        TestJob originalJob = new()
        {
            Message = "IShouldQueue cast test",
            HasExecuted = false
        };
        string serialized = SerializationHelper.Serialize(originalJob);

        // Act — deserialize as object (same as QueueWorker does)
        object deserialized = SerializationHelper.Deserialize<object>(serialized);

        // Assert — the safety gate: deserialized object IS an IShouldQueue
        Assert.True(deserialized is IShouldQueue, "Deserialized job should implement IShouldQueue");
        IShouldQueue queueable = (IShouldQueue)deserialized;
        Assert.NotNull(queueable);
    }

    [Fact]
    public void Deserialize_NonIShouldQueueType_FailsInterfaceCheck()
    {
        // Arrange — serialize a type that does NOT implement IShouldQueue
        NotAJob notAJob = new() { Data = "not a real job" };
        string serialized = SerializationHelper.Serialize(notAJob);

        // Act — deserialize as object (same as QueueWorker does)
        object deserialized = SerializationHelper.Deserialize<object>(serialized);

        // Assert — the safety gate: deserialized object is NOT an IShouldQueue
        Assert.False(deserialized is IShouldQueue, "Non-IShouldQueue type must not pass the interface check");
    }

    [Fact]
    public async Task Deserialize_IShouldQueueJob_ExecutesSuccessfully()
    {
        // Arrange — round-trip a job through serialize/deserialize
        TestJob originalJob = new()
        {
            Message = "Execute after deserialize",
            HasExecuted = false
        };
        string serialized = SerializationHelper.Serialize(originalJob);

        // Act — deserialize and execute via the IShouldQueue interface
        object deserialized = SerializationHelper.Deserialize<object>(serialized);
        Assert.True(deserialized is IShouldQueue);
        IShouldQueue queueable = (IShouldQueue)deserialized;
        await queueable.Handle();

        // Assert — job actually ran
        TestJob executedJob = (TestJob)deserialized;
        Assert.True(executedJob.HasExecuted);
    }
}
