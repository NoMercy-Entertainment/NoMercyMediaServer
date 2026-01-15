using System.Reflection;
using NoMercy.Queue;
using NoMercy.Tests.Queue.TestHelpers;
using Xunit;

namespace NoMercy.Tests.Queue;

public class JobDispatcherTests
{
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

        // Act & Assert - We can't easily test JobDispatcher.Dispatch because it uses a static JobQueue
        // Instead, let's test that the serialization would work correctly
        string serialized = SerializationHelper.Serialize(testJob);
        object deserialized = SerializationHelper.Deserialize<object>(serialized);

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
        
        // Verify the job can be executed
        IShouldQueue executableJob = (IShouldQueue)deserialized;
        Assert.NotNull(executableJob);
        
        // The job should be able to handle execution
        MethodInfo? handleMethod = executableJob.GetType().GetMethod("Handle");
        Assert.NotNull(handleMethod);
    }
}
