namespace NoMercy.Encoder.Distribution;

/// <summary>
/// Wire format for <see cref="EncodeTask"/> payloads sent between
/// coordinator and remote workers. Mirrors <see cref="Jobs.IJobSerializer"/>
/// — HMAC-SHA256 signed envelope with a short-lived timestamp so replay
/// attacks and tampered FFmpeg arguments are caught at the worker before
/// anything spawns.
/// </summary>
public interface ITaskSerializer
{
    string Serialize(EncodeTask task, byte[] signingKey);

    EncodeTask? Deserialize(string payload, byte[] signingKey);

    string SerializeResult(DispatchResult result, byte[] signingKey);

    DispatchResult? DeserializeResult(string payload, byte[] signingKey);
}
