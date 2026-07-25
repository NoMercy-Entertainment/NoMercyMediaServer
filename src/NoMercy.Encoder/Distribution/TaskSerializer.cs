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
    private static readonly TimeSpan MaxPayloadAge = TimeSpan.FromMinutes(5);

    public string Serialize(EncodeTask task, byte[] signingKey)
    {
        SignedPayload payload = new(Task: task, TimestampUtc: DateTime.UtcNow);
        return SignAndWrap(payload, signingKey);
    }

    public EncodeTask? Deserialize(string payload, byte[] signingKey)
    {
        SignedPayload? signed = VerifyAndUnwrap<SignedPayload>(payload, signingKey);
        if (signed is null)
            return null;

        if (DateTime.UtcNow - signed.TimestampUtc > MaxPayloadAge)
            return null; // Expired — reject replay attempts.

        return signed.Task;
    }

    public string SerializeResult(DispatchResult result, byte[] signingKey)
    {
        SignedResultPayload payload = new(Result: result, TimestampUtc: DateTime.UtcNow);
        return SignAndWrap(payload, signingKey);
    }

    public DispatchResult? DeserializeResult(string payload, byte[] signingKey)
    {
        SignedResultPayload? signed = VerifyAndUnwrap<SignedResultPayload>(payload, signingKey);
        if (signed is null)
            return null;

        if (DateTime.UtcNow - signed.TimestampUtc > MaxPayloadAge)
            return null;

        return signed.Result;
    }

    private static string SignAndWrap<T>(T payload, byte[] signingKey)
    {
        string json = JsonConvert.SerializeObject(payload);
        string signature = ComputeHmac(json, signingKey);
        SignedEnvelope envelope = new(json, signature);
        return JsonConvert.SerializeObject(envelope);
    }

    private static T? VerifyAndUnwrap<T>(string payload, byte[] signingKey)
        where T : class
    {
        if (string.IsNullOrEmpty(payload))
            return null;

        SignedEnvelope? envelope;
        try
        {
            envelope = JsonConvert.DeserializeObject<SignedEnvelope>(payload);
        }
        catch (JsonException)
        {
            return null;
        }

        if (envelope is null)
            return null;
        if (string.IsNullOrEmpty(envelope.Payload) || string.IsNullOrEmpty(envelope.Signature))
            return null;

        string expected = ComputeHmac(envelope.Payload, signingKey);
        if (
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(envelope.Signature),
                Encoding.UTF8.GetBytes(expected)
            )
        )
            return null;

        try
        {
            return JsonConvert.DeserializeObject<T>(envelope.Payload);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ComputeHmac(string data, byte[] key)
    {
        using HMACSHA256 hmac = new(key);
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }

    private sealed record SignedPayload(EncodeTask Task, DateTime TimestampUtc);

    private sealed record SignedResultPayload(DispatchResult Result, DateTime TimestampUtc);

    private sealed record SignedEnvelope(string Payload, string Signature);
}
