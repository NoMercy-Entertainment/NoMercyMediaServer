namespace NoMercy.Encoder.V3.Jobs;

public interface IJobSerializer
{
    string Serialize(EncodingJob job, byte[] signingKey);
    EncodingJob? Deserialize(string payload, byte[] signingKey);
}
