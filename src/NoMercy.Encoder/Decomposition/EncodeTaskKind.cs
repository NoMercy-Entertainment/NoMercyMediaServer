namespace NoMercy.Encoder.Decomposition;

/// <summary>
/// Identifies what kind of work a single decomposed encode task represents.
/// Used by the coordinator to display per-task progress rows on the dashboard
/// and to route tasks to the correct queue workers.
/// </summary>
public enum EncodeTaskKind
{
    /// <summary>One video rung (resolution / bitrate tier).</summary>
    Video,

    /// <summary>One audio mix group (language + channel layout).</summary>
    Audio,

    /// <summary>One subtitle track extraction or conversion.</summary>
    Subtitle,

    /// <summary>Sprite VTT thumbnail sheet.</summary>
    Thumbnails,

    /// <summary>Chapter VTT / still extraction (post-encode).</summary>
    Chapters,

    /// <summary>
    /// First pass of a two-pass encode. Produces a stats file consumed
    /// by the matching <see cref="Pass2"/> tasks.
    /// </summary>
    Pass1,

    /// <summary>
    /// Second pass of a two-pass encode. Requires its corresponding
    /// <see cref="Pass1"/> task to complete first.
    /// </summary>
    Pass2,

    /// <summary>
    /// The whole plan as a single undivided unit. Returned by strategies
    /// that cannot or do not benefit from splitting (MP4 single-pass, MKV).
    /// </summary>
    Whole,
}
