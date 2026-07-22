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

namespace NoMercy.Encoder.Jobs;

public class JobSerializer : IJobSerializer
{
    private static readonly TimeSpan MaxPayloadAge = TimeSpan.FromMinutes(minutes: 5);

    public string Serialize(EncodingJob job, byte[] signingKey)
    {
        SignedPayload payload = new(Job: job, TimestampUtc: DateTime.UtcNow);

        string json = JsonConvert.SerializeObject(value: payload);
        string signature = ComputeHmac(data: json, key: signingKey);

        SignedEnvelope envelope = new(Payload: json, Signature: signature);
        return JsonConvert.SerializeObject(value: envelope);
    }

    public EncodingJob? Deserialize(string payload, byte[] signingKey)
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
            // Malformed JSON — reject cleanly instead of surfacing the parse
            // exception. Remote workers feed arbitrary bytes into this entry
            // point so parse failures are a normal "rejected" signal, not a
            // crash.
            return null;
        }

        if (envelope is null)
            return null;

        // Null payload/signature means the envelope was deserialized from
        // partial JSON (e.g. "{}") — treat as rejected. Without this check
        // the HMAC comparison below would NRE on Encoding.UTF8.GetBytes(null).
        if (string.IsNullOrEmpty(value: envelope.Payload) || string.IsNullOrEmpty(value: envelope.Signature))
            return null;

        // Verify HMAC
        string expectedSignature = ComputeHmac(data: envelope.Payload, key: signingKey);
        if (
            !CryptographicOperations.FixedTimeEquals(
                left: Encoding.UTF8.GetBytes(s: envelope.Signature),
                right: Encoding.UTF8.GetBytes(s: expectedSignature)
            )
        )
        {
            return null; // Tampered
        }

        // Verify timestamp
        SignedPayload? signed = JsonConvert.DeserializeObject<SignedPayload>(value: envelope.Payload);
        if (signed is null)
            return null;

        if (DateTime.UtcNow - signed.TimestampUtc > MaxPayloadAge)
        {
            return null; // Expired
        }

        return signed.Job;
    }

    private static string ComputeHmac(string data, byte[] key)
    {
        using HMACSHA256 hmac = new(key: key);
        byte[] hash = hmac.ComputeHash(buffer: Encoding.UTF8.GetBytes(s: data));
        return Convert.ToBase64String(inArray: hash);
    }

    private record SignedPayload(EncodingJob Job, DateTime TimestampUtc);

    private record SignedEnvelope(string Payload, string Signature);
}
