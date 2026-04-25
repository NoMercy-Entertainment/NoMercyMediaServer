namespace NoMercy.Encoder.Subtitles;

/// <summary>
/// The filter chain produced by <see cref="PgsBurnInFilterBuilder"/>.
/// </summary>
/// <param name="FilterComplex">
/// The <c>-filter_complex</c> value, e.g.
/// <c>[0:v:0][0:s:1]overlay=format=auto[burned]</c>.
/// </param>
/// <param name="MapLabel">
/// The output pad label the caller should pass to <c>-map</c>, e.g.
/// <c>[burned]</c>. Using this label in place of the original video
/// stream selector routes the composited output to the encoder.
/// </param>
public record PgsBurnInFilterChain(string FilterComplex, string MapLabel);

/// <summary>
/// Builds a <c>-filter_complex</c> chain that overlays PGS (bitmap)
/// subtitles onto the video stream using the FFmpeg <c>overlay</c> filter.
///
/// <para>The <c>subtitles</c> filter cannot decode PGS; instead FFmpeg
/// decodes the subtitle stream to raw RGBA bitmaps and the
/// <c>overlay=format=auto</c> filter composites them onto the video frame
/// at the correct timestamp. The resulting pad label replaces the original
/// video stream selector in <c>-map</c>.</para>
///
/// <para>Example output for video index 0, subtitle index 1:</para>
/// <code>[0:v:0][0:s:1]overlay=format=auto[burned]</code>
/// </summary>
public sealed class PgsBurnInFilterBuilder
{
    /// <summary>
    /// Builds the PGS overlay filter chain.
    /// </summary>
    /// <param name="videoStreamIndex">
    /// Index of the video stream within the first input (0-based). Almost
    /// always <c>0</c> for single-video sources.
    /// </param>
    /// <param name="subtitleStreamIndex">
    /// Index of the subtitle stream within the first input (0-based),
    /// matching <see cref="NoMercy.Encoder.Output.SubtitleOutputPlan.SourceIndex"/>.
    /// </param>
    /// <returns>
    /// A <see cref="PgsBurnInFilterChain"/> whose
    /// <see cref="PgsBurnInFilterChain.FilterComplex"/> can be passed
    /// directly to <c>-filter_complex</c> and whose
    /// <see cref="PgsBurnInFilterChain.MapLabel"/> replaces the video
    /// stream in <c>-map</c>.
    /// </returns>
    public PgsBurnInFilterChain Build(int videoStreamIndex, int subtitleStreamIndex)
    {
        string filterComplex =
            $"[0:v:{videoStreamIndex}][0:s:{subtitleStreamIndex}]overlay=format=auto[burned]";

        return new PgsBurnInFilterChain(FilterComplex: filterComplex, MapLabel: "[burned]");
    }
}
