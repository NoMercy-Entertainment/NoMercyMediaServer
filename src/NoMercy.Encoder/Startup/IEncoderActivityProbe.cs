namespace NoMercy.Encoder.Startup;

/// <summary>
/// Lets the deferred hardware benchmark ask the host whether the encoder is
/// currently doing user-visible work (live transcode session, queued encode
/// job). The Encoder project can't see the queue or streaming session
/// manager directly, so a concrete probe lives in the hosting project and
/// is injected here. Default implementation returns <c>false</c> so test
/// harnesses and standalone encoder uses behave unchanged.
/// </summary>
public interface IEncoderActivityProbe
{
    bool IsBusy { get; }
}

internal sealed class NullEncoderActivityProbe : IEncoderActivityProbe
{
    public bool IsBusy => false;
}
