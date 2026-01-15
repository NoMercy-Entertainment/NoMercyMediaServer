using System.Reflection;
using NoMercy.Queue;
using NoMercy.Tests.Queue.TestHelpers;
using Xunit;

namespace NoMercy.Tests.Queue;

public class InterfaceTests
{
    [Fact]
    public void IShouldQueue_TestJob_ImplementsCorrectly()
    {
        // Arrange
        TestJob testJob = new();

        // Act & Assert
        Assert.IsAssignableFrom<IShouldQueue>(testJob);
        
        // Verify the Handle method exists and has correct signature
        MethodInfo? handleMethod = typeof(TestJob).GetMethod("Handle");
        Assert.NotNull(handleMethod);
        Assert.Equal(typeof(Task), handleMethod.ReturnType);
    }

    [Fact]
    public void IShouldQueue_AnotherTestJob_ImplementsCorrectly()
    {
        // Arrange
        AnotherTestJob testJob = new();

        // Act & Assert
        Assert.IsAssignableFrom<IShouldQueue>(testJob);
        
        // Verify the Handle method exists and has correct signature
        MethodInfo? handleMethod = typeof(AnotherTestJob).GetMethod("Handle");
        Assert.NotNull(handleMethod);
        Assert.Equal(typeof(Task), handleMethod.ReturnType);
    }

    [Fact]
    public async Task IShouldQueue_CanBeExecutedPolymorphically()
    {
        // Arrange
        IShouldQueue[] jobs = 
        [
            new TestJob { Message = "Polymorphic test 1" },
            new AnotherTestJob { Value = 42 }
        ];

        // Act
        foreach (IShouldQueue job in jobs)
        {
            await job.Handle();
        }

        // Assert
        TestJob testJob = (TestJob)jobs[0];
        AnotherTestJob anotherJob = (AnotherTestJob)jobs[1];
        
        Assert.True(testJob.HasExecuted);
        Assert.Equal("Polymorphic test 1", testJob.Message);
        Assert.True(anotherJob.HasExecuted);
        Assert.Equal(84, anotherJob.Value); // Should be doubled
    }
}

public class EdgeCaseTests
{
    [Fact]
    public void SerializationHelper_EmptyObject_HandlesCorrectly()
    {
        // Arrange
        TestJob emptyJob = new(); // Default values

        // Act
        string serialized = SerializationHelper.Serialize(emptyJob);
        TestJob deserialized = SerializationHelper.Deserialize<TestJob>(serialized);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(emptyJob.HasExecuted, deserialized.HasExecuted);
        Assert.Equal(emptyJob.ShouldFail, deserialized.ShouldFail);
    }

    [Fact]
    public void SerializationHelper_LargeString_HandlesCorrectly()
    {
        // Arrange
        string largeMessage = new('A', 10000); // 10KB string
        TestJob testJob = new() { Message = largeMessage };

        // Act
        string serialized = SerializationHelper.Serialize(testJob);
        TestJob deserialized = SerializationHelper.Deserialize<TestJob>(serialized);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(largeMessage, deserialized.Message);
    }

    [Fact]
    public void SerializationHelper_SpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        string specialMessage = "Test with special chars: \n\r\t\"'\\{}[]()<>!@#$%^&*+=|~`";
        TestJob testJob = new() { Message = specialMessage };

        // Act
        string serialized = SerializationHelper.Serialize(testJob);
        TestJob deserialized = SerializationHelper.Deserialize<TestJob>(serialized);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(specialMessage, deserialized.Message);
    }

    [Fact]
    public void SerializationHelper_UnicodeCharacters_HandlesCorrectly()
    {
        // Arrange
        string unicodeMessage = "Unicode test: 你好世界 🌍 ñáéíóú Ω α β γ δ";
        TestJob testJob = new() { Message = unicodeMessage };

        // Act
        string serialized = SerializationHelper.Serialize(testJob);
        TestJob deserialized = SerializationHelper.Deserialize<TestJob>(serialized);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(unicodeMessage, deserialized.Message);
    }
}

public class StressTests
{
    [Fact]
    public void SerializationHelper_MultipleSerializationCycles_MaintainsIntegrity()
    {
        // Arrange
        TestJob originalJob = new()
        { 
            Message = "Stress test job",
            HasExecuted = true
        };

        // Act - Serialize and deserialize multiple times
        object currentJob = originalJob;
        for (int i = 0; i < 100; i++)
        {
            string serialized = SerializationHelper.Serialize(currentJob);
            currentJob = SerializationHelper.Deserialize<TestJob>(serialized);
        }

        // Assert
        TestJob finalJob = (TestJob)currentJob;
        Assert.Equal(originalJob.Message, finalJob.Message);
        Assert.Equal(originalJob.HasExecuted, finalJob.HasExecuted);
        Assert.Equal(originalJob.ShouldFail, finalJob.ShouldFail);
    }
}
