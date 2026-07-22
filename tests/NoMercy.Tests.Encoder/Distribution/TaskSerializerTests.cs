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

using System.Text;
using NoMercy.Encoder.Distribution;

namespace NoMercy.Tests.Encoder.Distribution;

public class TaskSerializerTests
{
    private readonly byte[] _signingKey = Encoding.UTF8.GetBytes(
        s: "test-task-signing-key-32-bytes-!"
    );
    private readonly TaskSerializer _serializer = new();

    [Fact]
    public void RoundTrip_Task_MatchesOriginal()
    {
        EncodeTask task = MakeTask();

        string wire = _serializer.Serialize(task: task, signingKey: _signingKey);
        EncodeTask? decoded = _serializer.Deserialize(payload: wire, signingKey: _signingKey);

        decoded.Should().NotBeNull();
        decoded!.TaskId.Should().Be(expected: task.TaskId);
        decoded.OutputPath.Should().Be(expected: task.OutputPath);
        decoded.Type.Should().Be(expected: task.Type);
    }

    [Fact]
    public void Deserialize_WrongKey_ReturnsNull()
    {
        EncodeTask task = MakeTask();
        string wire = _serializer.Serialize(task: task, signingKey: _signingKey);

        byte[] wrongKey = Encoding.UTF8.GetBytes(s: "wrong-task-signing-key-32-bytes!");
        EncodeTask? result = _serializer.Deserialize(payload: wire, signingKey: wrongKey);

        result.Should().BeNull();
    }

    [Fact]
    public void Deserialize_Tampered_ReturnsNull()
    {
        EncodeTask task = MakeTask(id: "t0");
        string wire = _serializer.Serialize(task: task, signingKey: _signingKey);

        string tampered = wire.Replace(oldValue: "t0", newValue: "evil");
        EncodeTask? result = _serializer.Deserialize(payload: tampered, signingKey: _signingKey);

        result.Should().BeNull();
    }

    [Fact]
    public void Deserialize_MalformedJson_ReturnsNull()
    {
        _serializer.Deserialize(payload: "not json", signingKey: _signingKey).Should().BeNull();
    }

    [Fact]
    public void Deserialize_Empty_ReturnsNull()
    {
        _serializer.Deserialize(payload: "", signingKey: _signingKey).Should().BeNull();
    }

    [Fact]
    public void Deserialize_EmptyJsonObject_ReturnsNull()
    {
        // Missing Payload and Signature fields — must not NRE.
        _serializer.Deserialize(payload: "{}", signingKey: _signingKey).Should().BeNull();
    }

    [Fact]
    public void RoundTrip_DispatchResult_MatchesOriginal()
    {
        DispatchResult original = new(
            TaskId: "t0",
            Success: true,
            OutputPath: "/out/t0.ts",
            Duration: TimeSpan.FromSeconds(seconds: 5),
            Error: null,
            WorkerId: "beast"
        );

        string wire = _serializer.SerializeResult(result: original, signingKey: _signingKey);
        DispatchResult? decoded = _serializer.DeserializeResult(payload: wire, signingKey: _signingKey);

        decoded.Should().NotBeNull();
        decoded!.TaskId.Should().Be(expected: "t0");
        decoded.Success.Should().BeTrue();
        decoded.OutputPath.Should().Be(expected: "/out/t0.ts");
        decoded.WorkerId.Should().Be(expected: "beast");
    }

    [Fact]
    public void DeserializeResult_WrongKey_ReturnsNull()
    {
        DispatchResult original = new(TaskId: "t0", Success: true, OutputPath: "/out", Duration: TimeSpan.FromSeconds(seconds: 1));
        string wire = _serializer.SerializeResult(result: original, signingKey: _signingKey);

        byte[] wrongKey = Encoding.UTF8.GetBytes(s: "different-key-32-bytes-long-pad!");
        _serializer.DeserializeResult(payload: wire, signingKey: wrongKey).Should().BeNull();
    }

    private static EncodeTask MakeTask(string id = "task-1") =>
        new(
            TaskId: id,
            Command: new(Executable: "ffmpeg", Arguments: ["-i", "in.mkv", "out.ts"], WorkingDirectory: null),
            OutputPath: $"/out/{id}",
            Type: EncodeTaskType.QualityVariant
        );
}
