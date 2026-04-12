namespace NoMercy.Tests.Encoder.V3.Pipeline;

using NoMercy.Encoder.V3.Errors;
using NoMercy.Encoder.V3.Pipeline;

public class StageResultTests
{
    [Fact]
    public void Success_ContainsValue()
    {
        StageResult result = new StageSuccess<string>("hello");
        result.Should().BeOfType<StageSuccess<string>>();
        StageSuccess<string> success = (StageSuccess<string>)result;
        success.Value.Should().Be("hello");
    }

    [Fact]
    public void Failure_ContainsError()
    {
        EncodingError error = new(
            EncodingErrorKind.InputNotFound,
            "not found",
            null,
            "Analyze",
            false
        );
        StageResult result = new StageFailure(error);
        result.Should().BeOfType<StageFailure>();
        StageFailure failure = (StageFailure)result;
        failure.Error.Kind.Should().Be(EncodingErrorKind.InputNotFound);
    }

    [Fact]
    public void PatternMatch_Works()
    {
        StageResult success = new StageSuccess<int>(42);
        string output = success switch
        {
            StageSuccess<int> s => $"got {s.Value}",
            StageFailure f => $"error: {f.Error.Message}",
            _ => "unknown",
        };
        output.Should().Be("got 42");
    }
}
