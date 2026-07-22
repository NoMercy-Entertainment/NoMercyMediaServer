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
using NoMercy.Tests.Queue.TestHelpers;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
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
        Assert.IsAssignableFrom<IShouldQueue>(@object: testJob);

        // Verify the Handle method exists and has correct signature
        MethodInfo? handleMethod = typeof(TestJob).GetMethod(name: "Handle");
        Assert.NotNull(@object: handleMethod);
        Assert.Equal(expected: typeof(Task), actual: handleMethod.ReturnType);
    }

    [Fact]
    public void IShouldQueue_AnotherTestJob_ImplementsCorrectly()
    {
        // Arrange
        AnotherTestJob testJob = new();

        // Act & Assert
        Assert.IsAssignableFrom<IShouldQueue>(@object: testJob);

        // Verify the Handle method exists and has correct signature
        MethodInfo? handleMethod = typeof(AnotherTestJob).GetMethod(name: "Handle");
        Assert.NotNull(@object: handleMethod);
        Assert.Equal(expected: typeof(Task), actual: handleMethod.ReturnType);
    }

    [Fact]
    public async Task IShouldQueue_CanBeExecutedPolymorphically()
    {
        // Arrange
        IShouldQueue[] jobs =
        [
            new TestJob { Message = "Polymorphic test 1" },
            new AnotherTestJob { Value = 42 },
        ];

        // Act
        foreach (IShouldQueue job in jobs)
        {
            await job.Handle();
        }

        // Assert
        TestJob testJob = (TestJob)jobs[0];
        AnotherTestJob anotherJob = (AnotherTestJob)jobs[1];

        Assert.True(condition: testJob.HasExecuted);
        Assert.Equal(expected: "Polymorphic test 1", actual: testJob.Message);
        Assert.True(condition: anotherJob.HasExecuted);
        Assert.Equal(expected: 84, actual: anotherJob.Value); // Should be doubled
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
        string serialized = SerializationHelper.Serialize(obj: emptyJob);
        TestJob deserialized = SerializationHelper.Deserialize<TestJob>(data: serialized);

        // Assert
        Assert.NotNull(@object: deserialized);
        Assert.Equal(expected: emptyJob.HasExecuted, actual: deserialized.HasExecuted);
        Assert.Equal(expected: emptyJob.ShouldFail, actual: deserialized.ShouldFail);
    }

    [Fact]
    public void SerializationHelper_LargeString_HandlesCorrectly()
    {
        // Arrange
        string largeMessage = new(c: 'A', count: 10000); // 10KB string
        TestJob testJob = new() { Message = largeMessage };

        // Act
        string serialized = SerializationHelper.Serialize(obj: testJob);
        TestJob deserialized = SerializationHelper.Deserialize<TestJob>(data: serialized);

        // Assert
        Assert.NotNull(@object: deserialized);
        Assert.Equal(expected: largeMessage, actual: deserialized.Message);
    }

    [Fact]
    public void SerializationHelper_SpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        string specialMessage = "Test with special chars: \n\r\t\"'\\{}[]()<>!@#$%^&*+=|~`";
        TestJob testJob = new() { Message = specialMessage };

        // Act
        string serialized = SerializationHelper.Serialize(obj: testJob);
        TestJob deserialized = SerializationHelper.Deserialize<TestJob>(data: serialized);

        // Assert
        Assert.NotNull(@object: deserialized);
        Assert.Equal(expected: specialMessage, actual: deserialized.Message);
    }

    [Fact]
    public void SerializationHelper_UnicodeCharacters_HandlesCorrectly()
    {
        // Arrange
        string unicodeMessage = "Unicode test: 你好世界 🌍 ñáéíóú Ω α β γ δ";
        TestJob testJob = new() { Message = unicodeMessage };

        // Act
        string serialized = SerializationHelper.Serialize(obj: testJob);
        TestJob deserialized = SerializationHelper.Deserialize<TestJob>(data: serialized);

        // Assert
        Assert.NotNull(@object: deserialized);
        Assert.Equal(expected: unicodeMessage, actual: deserialized.Message);
    }
}

public class StressTests
{
    [Fact]
    public void SerializationHelper_MultipleSerializationCycles_MaintainsIntegrity()
    {
        // Arrange
        TestJob originalJob = new() { Message = "Stress test job", HasExecuted = true };

        // Act - Serialize and deserialize multiple times
        object currentJob = originalJob;
        for (int i = 0; i < 100; i++)
        {
            string serialized = SerializationHelper.Serialize(obj: currentJob);
            currentJob = SerializationHelper.Deserialize<TestJob>(data: serialized);
        }

        // Assert
        TestJob finalJob = (TestJob)currentJob;
        Assert.Equal(expected: originalJob.Message, actual: finalJob.Message);
        Assert.Equal(expected: originalJob.HasExecuted, actual: finalJob.HasExecuted);
        Assert.Equal(expected: originalJob.ShouldFail, actual: finalJob.ShouldFail);
    }
}
