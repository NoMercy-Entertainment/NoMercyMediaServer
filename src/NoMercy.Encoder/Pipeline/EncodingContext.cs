namespace NoMercy.Encoder.Pipeline;

using NoMercy.Encoder.Analysis;

public record EncodingContext(string CorrelationId, MediaInfo? MediaInfo = null)
{
    public static EncodingContext Create()
    {
        return new EncodingContext(CorrelationId: Ulid.NewUlid().ToString());
    }
}
