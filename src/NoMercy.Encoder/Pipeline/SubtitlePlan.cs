namespace NoMercy.Encoder.Pipeline;

/// <summary>
/// Spec-shape for one subtitle stream in the encoding plan. The dashboard
/// renders subtitle plans in a separate table below the variant cards so
/// users can see which tracks will be extracted, burned-in, or OCR'd
/// before committing to the encode.
/// </summary>
public sealed record SubtitlePlan(
    int SourceIndex,
    string Codec,
    string? Language,
    /// <summary>
    /// One of <c>copy</c>, <c>extract</c>, <c>extract_ocr</c>, or <c>burn_in</c>.
    /// Derived from <see cref="NoMercy.Encoder.Pipeline.StreamAction"/> and
    /// <see cref="NoMercy.Encoder.Profiles.SubtitleMode"/>.
    /// </summary>
    string Action
);
