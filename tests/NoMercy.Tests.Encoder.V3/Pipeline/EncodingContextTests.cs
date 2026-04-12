namespace NoMercy.Tests.Encoder.V3.Pipeline;

using NoMercy.Encoder.V3.Pipeline;

public class EncodingContextTests
{
    [Fact]
    public void Context_HasCorrelationId()
    {
        EncodingContext ctx = EncodingContext.Create();
        ctx.CorrelationId.Should().NotBeNullOrEmpty();
        ctx.CorrelationId.Should().HaveLength(26); // ULID length
    }

    [Fact]
    public void Context_TwoInstances_HaveDifferentIds()
    {
        EncodingContext ctx1 = EncodingContext.Create();
        EncodingContext ctx2 = EncodingContext.Create();
        ctx1.CorrelationId.Should().NotBe(ctx2.CorrelationId);
    }
}
