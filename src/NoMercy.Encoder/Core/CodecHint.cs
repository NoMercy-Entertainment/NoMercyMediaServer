namespace NoMercy.Encoder.Core;

/// <summary>
///     Identifies the target encoder so a quality scaler can apply per-encoder quirks (range
///     offsets, clamping, inverted semantics) on top of the basic source-to-target scale
///     conversion. Aligns with the journal's spec for <c>The Effortless Encoder</c>: every
///     encoder family maps the same logical "I want CRF 23" intent to whatever native scale the
///     chosen encoder accepts.
/// </summary>
public enum CodecHint
{
    Unknown,

    SoftwareH264,
    SoftwareHevc,
    SoftwareAv1,
    SoftwareVp9,

    NvencH264,
    NvencHevc,
    NvencAv1,

    QsvH264,
    QsvHevc,
    QsvAv1,
    QsvVp9,

    AmfH264,
    AmfHevc,
    AmfAv1,

    VaapiH264,
    VaapiHevc,
    VaapiAv1,
    VaapiVp9,

    VideoToolboxH264,
    VideoToolboxHevc,
}
