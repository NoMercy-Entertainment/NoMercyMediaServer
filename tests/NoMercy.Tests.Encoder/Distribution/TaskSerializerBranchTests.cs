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
using Newtonsoft.Json;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Distribution;

namespace NoMercy.Tests.Encoder.Distribution;

/// <summary>
/// Branch-coverage gaps for <see cref="TaskSerializer"/> beyond the round-trip
/// + tamper + wrong-key cases in <see cref="TaskSerializerTests"/>:
///
/// • Replay-window enforcement — payloads older than 5 minutes are rejected
///   even when correctly signed. This is the only protection against a worker
///   replaying captured task envelopes after a credential rotation.
/// • Envelope shape edge cases — empty Payload or empty Signature fields are
///   rejected without NRE.
/// • DispatchResult tamper + replay parity — the SerializeResult /
///   DeserializeResult pair must enforce identical guarantees as the Task
///   pair (different test from existing wrong-key result test).
/// • Idempotent serialization — calling Serialize twice produces verifiably
///   different envelopes (different TimestampUtc) so a captured envelope
///   can't be re-shipped as-if-fresh.
/// </summary>
public class TaskSerializerBranchTests
{
    private readonly byte[] _signingKey = Encoding.UTF8.GetBytes(
        s: "test-task-signing-key-32-bytes-!"
    );
    private readonly TaskSerializer _serializer = new();

    // ── Replay-window enforcement ────────────────────────────────────────────

    [Fact]
    public void Deserialize_payload_older_than_five_minutes_returns_null()
    {
        // Hand-craft an envelope with a stale timestamp using the serializer's
        // own envelope shape, signed with the real key. The deserializer must
        // reject the timestamp even though the HMAC is valid.
        EncodeTask task = MakeTask();
        DateTime staleTs = DateTime.UtcNow.AddMinutes(value: -6);
        string inner = JsonConvert.SerializeObject(value: new { Task = task, TimestampUtc = staleTs });
        string envelope = SignEnvelope(inner: inner);

        EncodeTask? result = _serializer.Deserialize(payload: envelope, signingKey: _signingKey);

        result.Should().BeNull(because: "expired payloads must not be accepted even with valid HMAC");
    }

    [Fact]
    public void Deserialize_payload_exactly_at_replay_window_boundary_still_accepted()
    {
        // Anything ≤ 5 minutes old is accepted. Use 4 min 30s to dodge clock
        // jitter while still proving the boundary side.
        EncodeTask task = MakeTask();
        DateTime nearLimit = DateTime.UtcNow.AddSeconds(value: -270);
        string inner = JsonConvert.SerializeObject(value: new { Task = task, TimestampUtc = nearLimit });
        string envelope = SignEnvelope(inner: inner);

        EncodeTask? result = _serializer.Deserialize(payload: envelope, signingKey: _signingKey);

        result.Should().NotBeNull();
    }

    [Fact]
    public void DeserializeResult_payload_older_than_five_minutes_returns_null()
    {
        DispatchResult original = new(TaskId: "t0", Success: true, OutputPath: "/out", Duration: TimeSpan.FromSeconds(seconds: 1));
        DateTime staleTs = DateTime.UtcNow.AddMinutes(value: -6);
        string inner = JsonConvert.SerializeObject(
            value: new { Result = original, TimestampUtc = staleTs }
        );
        string envelope = SignEnvelope(inner: inner);

        DispatchResult? result = _serializer.DeserializeResult(payload: envelope, signingKey: _signingKey);

        result.Should().BeNull();
    }

    // ── Envelope shape edge cases ────────────────────────────────────────────

    [Fact]
    public void Deserialize_envelope_with_empty_payload_returns_null()
    {
        string envelope = JsonConvert.SerializeObject(value: new { Payload = "", Signature = "x" });

        _serializer.Deserialize(payload: envelope, signingKey: _signingKey).Should().BeNull();
    }

    [Fact]
    public void Deserialize_envelope_with_empty_signature_returns_null()
    {
        string envelope = JsonConvert.SerializeObject(value: new { Payload = "{}", Signature = "" });

        _serializer.Deserialize(payload: envelope, signingKey: _signingKey).Should().BeNull();
    }

    [Fact]
    public void Deserialize_envelope_with_both_empty_returns_null()
    {
        string envelope = JsonConvert.SerializeObject(value: new { Payload = "", Signature = "" });

        _serializer.Deserialize(payload: envelope, signingKey: _signingKey).Should().BeNull();
    }

    [Fact]
    public void Deserialize_envelope_with_payload_holding_invalid_inner_json_returns_null()
    {
        // Outer envelope deserializes fine, signature matches, but inner Payload
        // is itself malformed JSON. The second deserialize step inside
        // VerifyAndUnwrap must catch the JsonException.
        const string brokenInnerJson = "not json at all";
        string envelope = SignEnvelope(inner: brokenInnerJson);

        EncodeTask? result = _serializer.Deserialize(payload: envelope, signingKey: _signingKey);

        result.Should().BeNull();
    }

    // ── DispatchResult tamper ─────────────────────────────────────────────────

    [Fact]
    public void DeserializeResult_Tampered_ReturnsNull()
    {
        // Tamper a discriminator that's known to land in the wire ("beast"
        // is the worker id and shows up unescaped in the JSON-escaped Payload
        // field of the outer envelope).
        DispatchResult original = new(
            TaskId: "t0",
            Success: true,
            OutputPath: "/out/t0.ts",
            Duration: TimeSpan.FromSeconds(seconds: 5),
            Error: null,
            WorkerId: "beast"
        );

        string wire = _serializer.SerializeResult(result: original, signingKey: _signingKey);
        string tampered = wire.Replace(oldValue: "beast", newValue: "evil-");
        // Sanity: ensure the replace actually changed the wire.
        tampered.Should().NotBe(unexpected: wire);

        DispatchResult? result = _serializer.DeserializeResult(payload: tampered, signingKey: _signingKey);

        result.Should().BeNull(because: "tampered DispatchResult must not be accepted");
    }

    [Fact]
    public void DeserializeResult_MalformedJson_ReturnsNull()
    {
        _serializer.DeserializeResult(payload: "not json", signingKey: _signingKey).Should().BeNull();
    }

    [Fact]
    public void DeserializeResult_Empty_ReturnsNull()
    {
        _serializer.DeserializeResult(payload: "", signingKey: _signingKey).Should().BeNull();
    }

    // ── DispatchResult error round-trip ──────────────────────────────────────

    [Fact]
    public void RoundTrip_DispatchResult_with_error_message_preserves_error()
    {
        DispatchResult original = new(
            TaskId: "t0",
            Success: false,
            OutputPath: "",
            Duration: TimeSpan.FromMilliseconds(milliseconds: 123),
            Error: "ffmpeg returned exit code 1",
            WorkerId: null
        );

        string wire = _serializer.SerializeResult(result: original, signingKey: _signingKey);
        DispatchResult? decoded = _serializer.DeserializeResult(payload: wire, signingKey: _signingKey);

        decoded.Should().NotBeNull();
        decoded!.Success.Should().BeFalse();
        decoded.Error.Should().Be(expected: "ffmpeg returned exit code 1");
        decoded.WorkerId.Should().BeNull();
    }

    // ── Serialize repeatability ─────────────────────────────────────────────

    [Fact]
    public void Serialize_called_twice_produces_different_envelopes_due_to_timestamp()
    {
        // Each Serialize bakes DateTime.UtcNow into the inner payload — two
        // calls must produce different envelopes so a captured wire blob can't
        // be replayed as-if-fresh by attaching it to a new request.
        EncodeTask task = MakeTask();

        string wire1 = _serializer.Serialize(task: task, signingKey: _signingKey);
        // Windows DateTime.UtcNow resolution can drift up to ~15.6ms — sleep
        // 50ms so the second timestamp reliably differs from the first.
        Thread.Sleep(millisecondsTimeout: 50);
        string wire2 = _serializer.Serialize(task: task, signingKey: _signingKey);

        wire1.Should().NotBe(unexpected: wire2);

        // Both still decode successfully.
        _serializer.Deserialize(payload: wire1, signingKey: _signingKey).Should().NotBeNull();
        _serializer.Deserialize(payload: wire2, signingKey: _signingKey).Should().NotBeNull();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static EncodeTask MakeTask(string id = "task-1") =>
        new(
            TaskId: id,
            Command: new(Executable: "ffmpeg", Arguments: ["-i", "in.mkv", "out.ts"], WorkingDirectory: null),
            OutputPath: $"/out/{id}",
            Type: EncodeTaskType.QualityVariant
        );

    private string SignEnvelope(string inner)
    {
        string signature = ComputeHmac(data: inner, key: _signingKey);
        return JsonConvert.SerializeObject(value: new { Payload = inner, Signature = signature });
    }

    private static string ComputeHmac(string data, byte[] key)
    {
        using System.Security.Cryptography.HMACSHA256 hmac = new(key: key);
        byte[] hash = hmac.ComputeHash(buffer: Encoding.UTF8.GetBytes(s: data));
        return Convert.ToBase64String(inArray: hash);
    }
}
