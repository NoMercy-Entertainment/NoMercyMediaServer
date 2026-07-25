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
        "test-task-signing-key-32-bytes-!"
    );
    private readonly TaskSerializer _serializer = new();

    [Fact]
    public void RoundTrip_Task_MatchesOriginal()
    {
        EncodeTask task = MakeTask();

        string wire = _serializer.Serialize(task, _signingKey);
        EncodeTask? decoded = _serializer.Deserialize(wire, _signingKey);

        decoded.Should().NotBeNull();
        decoded!.TaskId.Should().Be(task.TaskId);
        decoded.OutputPath.Should().Be(task.OutputPath);
        decoded.Type.Should().Be(task.Type);
    }

    [Fact]
    public void Deserialize_WrongKey_ReturnsNull()
    {
        EncodeTask task = MakeTask();
        string wire = _serializer.Serialize(task, _signingKey);

        byte[] wrongKey = Encoding.UTF8.GetBytes("wrong-task-signing-key-32-bytes!");
        EncodeTask? result = _serializer.Deserialize(wire, wrongKey);

        result.Should().BeNull();
    }

    [Fact]
    public void Deserialize_Tampered_ReturnsNull()
    {
        EncodeTask task = MakeTask("t0");
        string wire = _serializer.Serialize(task, _signingKey);

        string tampered = wire.Replace("t0", "evil");
        EncodeTask? result = _serializer.Deserialize(tampered, _signingKey);

        result.Should().BeNull();
    }

    [Fact]
    public void Deserialize_MalformedJson_ReturnsNull()
    {
        _serializer.Deserialize("not json", _signingKey).Should().BeNull();
    }

    [Fact]
    public void Deserialize_Empty_ReturnsNull()
    {
        _serializer.Deserialize("", _signingKey).Should().BeNull();
    }

    [Fact]
    public void Deserialize_EmptyJsonObject_ReturnsNull()
    {
        // Missing Payload and Signature fields — must not NRE.
        _serializer.Deserialize("{}", _signingKey).Should().BeNull();
    }

    [Fact]
    public void RoundTrip_DispatchResult_MatchesOriginal()
    {
        DispatchResult original = new(
            "t0",
            true,
            "/out/t0.ts",
            TimeSpan.FromSeconds(5),
            null,
            "beast"
        );

        string wire = _serializer.SerializeResult(original, _signingKey);
        DispatchResult? decoded = _serializer.DeserializeResult(wire, _signingKey);

        decoded.Should().NotBeNull();
        decoded!.TaskId.Should().Be("t0");
        decoded.Success.Should().BeTrue();
        decoded.OutputPath.Should().Be("/out/t0.ts");
        decoded.WorkerId.Should().Be("beast");
    }

    [Fact]
    public void DeserializeResult_WrongKey_ReturnsNull()
    {
        DispatchResult original = new("t0", true, "/out", TimeSpan.FromSeconds(1));
        string wire = _serializer.SerializeResult(original, _signingKey);

        byte[] wrongKey = Encoding.UTF8.GetBytes("different-key-32-bytes-long-pad!");
        _serializer.DeserializeResult(wire, wrongKey).Should().BeNull();
    }

    private static EncodeTask MakeTask(string id = "task-1") =>
        new(
            id,
            new("ffmpeg", ["-i", "in.mkv", "out.ts"], null),
            $"/out/{id}",
            EncodeTaskType.QualityVariant
        );
}
