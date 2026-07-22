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

using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace NoMercy.Encoder.Distribution;

public class TaskSerializer : ITaskSerializer
{
    private static readonly TimeSpan MaxPayloadAge = TimeSpan.FromMinutes(minutes: 5);

    public string Serialize(EncodeTask task, byte[] signingKey)
    {
        SignedPayload payload = new(Task: task, TimestampUtc: DateTime.UtcNow);
        return SignAndWrap(payload: payload, signingKey: signingKey);
    }

    public EncodeTask? Deserialize(string payload, byte[] signingKey)
    {
        SignedPayload? signed = VerifyAndUnwrap<SignedPayload>(payload: payload, signingKey: signingKey);
        if (signed is null)
            return null;

        if (DateTime.UtcNow - signed.TimestampUtc > MaxPayloadAge)
            return null; // Expired — reject replay attempts.

        return signed.Task;
    }

    public string SerializeResult(DispatchResult result, byte[] signingKey)
    {
        SignedResultPayload payload = new(Result: result, TimestampUtc: DateTime.UtcNow);
        return SignAndWrap(payload: payload, signingKey: signingKey);
    }

    public DispatchResult? DeserializeResult(string payload, byte[] signingKey)
    {
        SignedResultPayload? signed = VerifyAndUnwrap<SignedResultPayload>(payload: payload, signingKey: signingKey);
        if (signed is null)
            return null;

        if (DateTime.UtcNow - signed.TimestampUtc > MaxPayloadAge)
            return null;

        return signed.Result;
    }

    private static string SignAndWrap<T>(T payload, byte[] signingKey)
    {
        string json = JsonConvert.SerializeObject(value: payload);
        string signature = ComputeHmac(data: json, key: signingKey);
        SignedEnvelope envelope = new(Payload: json, Signature: signature);
        return JsonConvert.SerializeObject(value: envelope);
    }

    private static T? VerifyAndUnwrap<T>(string payload, byte[] signingKey)
        where T : class
    {
        if (string.IsNullOrEmpty(value: payload))
            return null;

        SignedEnvelope? envelope;
        try
        {
            envelope = JsonConvert.DeserializeObject<SignedEnvelope>(value: payload);
        }
        catch (JsonException)
        {
            return null;
        }

        if (envelope is null)
            return null;
        if (string.IsNullOrEmpty(value: envelope.Payload) || string.IsNullOrEmpty(value: envelope.Signature))
            return null;

        string expected = ComputeHmac(data: envelope.Payload, key: signingKey);
        if (
            !CryptographicOperations.FixedTimeEquals(
                left: Encoding.UTF8.GetBytes(s: envelope.Signature),
                right: Encoding.UTF8.GetBytes(s: expected)
            )
        )
            return null;

        try
        {
            return JsonConvert.DeserializeObject<T>(value: envelope.Payload);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ComputeHmac(string data, byte[] key)
    {
        using HMACSHA256 hmac = new(key: key);
        byte[] hash = hmac.ComputeHash(buffer: Encoding.UTF8.GetBytes(s: data));
        return Convert.ToBase64String(inArray: hash);
    }

    private sealed record SignedPayload(EncodeTask Task, DateTime TimestampUtc);

    private sealed record SignedResultPayload(DispatchResult Result, DateTime TimestampUtc);

    private sealed record SignedEnvelope(string Payload, string Signature);
}
