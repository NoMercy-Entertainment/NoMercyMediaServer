namespace NoMercy.Encoder.V3.Pipeline;

using NoMercy.Encoder.V3.Analysis;

public record EncodingContext(string CorrelationId, MediaInfo? MediaInfo = null)
{
    public static EncodingContext Create()
    {
        return new EncodingContext(CorrelationId: Ulid.NewUlid().ToString());
    }
}
