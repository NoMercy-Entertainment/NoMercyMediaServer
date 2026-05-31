namespace NoMercy.Encoder.Jobs;

public interface IJobSerializer
{
    string Serialize(EncodingJob job, byte[] signingKey);
    EncodingJob? Deserialize(string payload, byte[] signingKey);
}
