namespace NoMercy.Encoder.V3.Jobs;

using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

public class JobSerializer : IJobSerializer
{
    private static readonly TimeSpan MaxPayloadAge = TimeSpan.FromMinutes(5);

    public string Serialize(EncodingJob job, byte[] signingKey)
    {
        SignedPayload payload = new(Job: job, TimestampUtc: DateTime.UtcNow);

        string json = JsonConvert.SerializeObject(payload);
        string signature = ComputeHmac(json, signingKey);

        SignedEnvelope envelope = new(json, signature);
        return JsonConvert.SerializeObject(envelope);
    }

    public EncodingJob? Deserialize(string payload, byte[] signingKey)
    {
        SignedEnvelope? envelope = JsonConvert.DeserializeObject<SignedEnvelope>(payload);
        if (envelope is null)
            return null;

        // Verify HMAC
        string expectedSignature = ComputeHmac(envelope.Payload, signingKey);
        if (
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(envelope.Signature),
                Encoding.UTF8.GetBytes(expectedSignature)
            )
        )
        {
            return null; // Tampered
        }

        // Verify timestamp
        SignedPayload? signed = JsonConvert.DeserializeObject<SignedPayload>(envelope.Payload);
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
        using HMACSHA256 hmac = new(key);
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }

    private record SignedPayload(EncodingJob Job, DateTime TimestampUtc);

    private record SignedEnvelope(string Payload, string Signature);
}
