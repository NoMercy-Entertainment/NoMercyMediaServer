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

using NoMercy.Tests.Queue.TestHelpers;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
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
            ShouldFail = false,
        };

        // Act
        string serialized = SerializationHelper.Serialize(obj: testJob);

        // Assert
        Assert.NotNull(@object: serialized);
        Assert.NotEmpty(collection: serialized);
        Assert.Contains(expectedSubstring: "Test message", actualString: serialized);
        Assert.Contains(expectedSubstring: "$type", actualString: serialized); // TypeNameHandling.All should include type info
    }

    [Fact]
    public void Deserialize_ValidJsonString_ReturnsCorrectObject()
    {
        // Arrange
        TestJob originalJob = new()
        {
            Message = "Original message",
            HasExecuted = true,
            ShouldFail = false,
        };
        string serialized = SerializationHelper.Serialize(obj: originalJob);

        // Act
        TestJob deserialized = SerializationHelper.Deserialize<TestJob>(data: serialized);

        // Assert
        Assert.NotNull(@object: deserialized);
        Assert.Equal(expected: originalJob.Message, actual: deserialized.Message);
        Assert.Equal(expected: originalJob.HasExecuted, actual: deserialized.HasExecuted);
        Assert.Equal(expected: originalJob.ShouldFail, actual: deserialized.ShouldFail);
    }

    [Fact]
    public void Serialize_Deserialize_ComplexJob_MaintainsIntegrity()
    {
        // Arrange
        AnotherTestJob originalJob = new() { Value = 42, HasExecuted = true };

        // Act
        string serialized = SerializationHelper.Serialize(obj: originalJob);
        AnotherTestJob deserialized = SerializationHelper.Deserialize<AnotherTestJob>(data: serialized);

        // Assert
        Assert.NotNull(@object: deserialized);
        Assert.Equal(expected: originalJob.Value, actual: deserialized.Value);
        Assert.Equal(expected: originalJob.HasExecuted, actual: deserialized.HasExecuted);
    }

    [Fact]
    public void Deserialize_AsObject_ReturnsCorrectType()
    {
        // Arrange
        TestJob originalJob = new() { Message = "Type test", HasExecuted = false };
        string serialized = SerializationHelper.Serialize(obj: originalJob);

        // Act
        object deserialized = SerializationHelper.Deserialize<object>(data: serialized);

        // Assert
        Assert.NotNull(@object: deserialized);
        Assert.IsType<TestJob>(@object: deserialized);
        TestJob testJob = (TestJob)deserialized;
        Assert.Equal(expected: originalJob.Message, actual: testJob.Message);
    }

    [Fact]
    public void Serialize_NullValues_HandlesCorrectly()
    {
        // Arrange
        TestJob testJob = new()
        {
            Message = null!, // Testing null handling
            HasExecuted = false,
        };

        // Act
        string serialized = SerializationHelper.Serialize(obj: testJob);
        TestJob deserialized = SerializationHelper.Deserialize<TestJob>(data: serialized);

        // Assert
        Assert.NotNull(@object: deserialized);
        // JSON.NET with NullValueHandling.Ignore actually omits null properties from serialization
        // but deserializes them as their default values (empty string for string, etc.)
        Assert.True(condition: string.IsNullOrEmpty(value: deserialized.Message));
        Assert.Equal(expected: testJob.HasExecuted, actual: deserialized.HasExecuted);
    }

    [Fact]
    public void Serialize_CamelCaseNaming_UsesCorrectFormat()
    {
        // Arrange
        TestJob testJob = new() { Message = "CamelCase test", HasExecuted = true };

        // Act
        string serialized = SerializationHelper.Serialize(obj: testJob);

        // Assert
        Assert.Contains(expectedSubstring: "hasExecuted", actualString: serialized); // Should be camelCase
        Assert.Contains(expectedSubstring: "message", actualString: serialized); // Should be camelCase
    }

    [Fact]
    public void Deserialize_IShouldQueueJob_CanBeCastToInterface()
    {
        // Arrange — serialize a valid job implementing IShouldQueue
        TestJob originalJob = new() { Message = "IShouldQueue cast test", HasExecuted = false };
        string serialized = SerializationHelper.Serialize(obj: originalJob);

        // Act — deserialize as object (same as QueueWorker does)
        object deserialized = SerializationHelper.Deserialize<object>(data: serialized);

        // Assert — the safety gate: deserialized object IS an IShouldQueue
        Assert.True(condition: deserialized is IShouldQueue, userMessage: "Deserialized job should implement IShouldQueue");
        IShouldQueue queueable = (IShouldQueue)deserialized;
        Assert.NotNull(@object: queueable);
    }

    [Fact]
    public void Deserialize_NonIShouldQueueType_FailsInterfaceCheck()
    {
        // Arrange — serialize a type that does NOT implement IShouldQueue
        NotAJob notAJob = new() { Data = "not a real job" };
        string serialized = SerializationHelper.Serialize(obj: notAJob);

        // Act — deserialize as object (same as QueueWorker does)
        object deserialized = SerializationHelper.Deserialize<object>(data: serialized);

        // Assert — the safety gate: deserialized object is NOT an IShouldQueue
        Assert.False(
            condition: deserialized is IShouldQueue,
            userMessage: "Non-IShouldQueue type must not pass the interface check"
        );
    }

    [Fact]
    public async Task Deserialize_IShouldQueueJob_ExecutesSuccessfully()
    {
        // Arrange — round-trip a job through serialize/deserialize
        TestJob originalJob = new() { Message = "Execute after deserialize", HasExecuted = false };
        string serialized = SerializationHelper.Serialize(obj: originalJob);

        // Act — deserialize and execute via the IShouldQueue interface
        object deserialized = SerializationHelper.Deserialize<object>(data: serialized);
        Assert.True(condition: deserialized is IShouldQueue);
        IShouldQueue queueable = (IShouldQueue)deserialized;
        await queueable.Handle();

        // Assert — job actually ran
        TestJob executedJob = (TestJob)deserialized;
        Assert.True(condition: executedJob.HasExecuted);
    }
}
