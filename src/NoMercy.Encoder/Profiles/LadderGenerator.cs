using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;

namespace NoMercy.Encoder.Profiles;

/// <summary>
/// Default implementation of <see cref="ILadderGenerator"/>.
///
/// <para>
/// Per-codec efficiency multipliers applied to the H.264 base bitrate:
///   HEVC  0.60 — well-established; matches Apple HLS spec and encoder guidance.
///   VP9   0.65 — midpoint of the 0.60–0.70 range from Google/YouTube benchmarks.
///   AV1   0.50 — AOMedia and Netflix data showing ~50 % savings vs H.264.
/// </para>
///
/// <para>
/// Source-resolution capping: rungs whose Width &gt; source.Width
/// OR Height &gt; source.Height are always skipped (no upscaling).
/// </para>
///
/// <para>
/// Non-standard source resolutions (e.g. 1440p when the table only lists
/// 1080p and 2160p): a native-resolution rung is appended at the top.
/// Its bitrate is interpolated from the two nearest table neighbours via
/// linear proportion in pixel-count space:
/// <c>bitrate = lower.BitrateKbps + (upper.BitrateKbps - lower.BitrateKbps)
///   * (srcPixels - lower.Pixels) / (upper.Pixels - lower.Pixels)</c>
/// When no upper neighbour exists (source exceeds the largest table entry),
/// the largest entry's bitrate is used directly.
/// </para>
/// </summary>
public class LadderGenerator : ILadderGenerator
{
    // Efficiency multipliers vs H.264 for codec-specific bitrate derivation.
    private const double HevcMultiplier = 0.60;
    private const double Vp9Multiplier = 0.65;
    private const double Av1Multiplier = 0.50;

    // H.264 reference bitrates in kbps.
    // Order: descending by height (widest first) so top-rung logic is natural.
    private static readonly TableRung[] DefaultTable =
    [
        new(Width: 3840, Height: 2160, H264BitrateKbps: 15000),
        new(Width: 2560, Height: 1440, H264BitrateKbps: 8000),
        new(Width: 1920, Height: 1080, H264BitrateKbps: 5000),
        new(Width: 1280, Height: 720, H264BitrateKbps: 3000),
        new(Width: 854, Height: 480, H264BitrateKbps: 1500),
        new(Width: 640, Height: 360, H264BitrateKbps: 800),
    ];

    public IReadOnlyList<VideoOutput> Generate(
        VideoOutput reference,
        VideoStreamInfo source,
        LadderRung[]? userRungs,
        IDecisionLogSink decisions,
        ComplexityHint complexity = ComplexityHint.Auto
    )
    {
        // ── User-supplied rungs ──────────────────────────────────────────────
        if (userRungs is { Length: > 0 })
        {
            decisions.Add(
                new DecisionLog(
                    Stage: "plan",
                    Key: "plan.ladder_user_supplied",
                    Message: $"Using {userRungs.Length} user-supplied rung(s); default table skipped."
                )
            );
            return userRungs
                .Select(r =>
                    reference with
                    {
                        Width = r.Width,
                        Height = r.Height,
                        BitrateKbps = r.BitrateKbps,
                        Crf = 0,
                    }
                )
                .ToArray();
        }

        // ── Complexity notice ────────────────────────────────────────────────
        if (complexity is ComplexityHint.Auto)
        {
            decisions.Add(
                new DecisionLog(
                    Stage: "plan",
                    Key: "plan.ladder_complexity_unknown",
                    Message: "Complexity is Auto — defaulted to live-action table. "
                        + "A scene-analysis probe will refine this in a later phase."
                )
            );
        }

        // ── Build rung set from default table ────────────────────────────────
        List<TableRung> candidates = [];
        foreach (TableRung rung in DefaultTable)
        {
            if (rung.Width <= source.Width && rung.Height <= source.Height)
            {
                candidates.Add(rung);
            }
            else
            {
                decisions.Add(
                    new DecisionLog(
                        Stage: "plan",
                        Key: "plan.ladder_rung_skipped_above_source",
                        Message: $"Rung {rung.Width}x{rung.Height} skipped — exceeds source "
                            + $"{source.Width}x{source.Height} (no upscaling)."
                    )
                );
            }
        }

        // ── Complexity-aware thinning ─────────────────────────────────────────
        if (complexity is ComplexityHint.Animated)
        {
            // Keep only even-indexed entries when sorted descending by height.
            // This retains the top rung plus every other step, which is sufficient
            // for flat-colour content that compresses aggressively.
            candidates = candidates.Where((_, i) => i % 2 == 0).ToList();
            decisions.Add(
                new DecisionLog(
                    Stage: "plan",
                    Key: "plan.ladder_animated_thinned",
                    Message: $"Animated complexity — thinned to every-other rung "
                        + $"({candidates.Count} rung(s)). Flat colour compresses well with fewer bitrate steps."
                )
            );
        }
        else if (complexity is ComplexityHint.Grainy)
        {
            // Keep all — grain requires denser bitrate exploration.
            decisions.Add(
                new DecisionLog(
                    Stage: "plan",
                    Key: "plan.ladder_grainy_full",
                    Message: $"Grainy complexity — retaining all {candidates.Count} fitting rung(s). "
                        + "Grain forces denser bitrate exploration."
                )
            );
        }

        // ── Append native-resolution top rung if not already in table ────────
        bool sourceMatchesTable = DefaultTable.Any(t =>
            t.Width == source.Width && t.Height == source.Height
        );

        if (!sourceMatchesTable && source.Width > 0 && source.Height > 0)
        {
            int nativeBitrate = InterpolateNativeBitrate(reference.Codec, source);
            candidates.Insert(
                0,
                new TableRung(
                    Width: source.Width,
                    Height: source.Height,
                    H264BitrateKbps: nativeBitrate
                )
            );
        }

        // ── Map table rungs → VideoOutput ─────────────────────────────────────
        List<VideoOutput> outputs = [];
        foreach (TableRung rung in candidates)
        {
            int bitrateKbps = PickBitrate(reference.Codec, rung);
            outputs.Add(
                reference with
                {
                    Width = rung.Width,
                    Height = rung.Height,
                    BitrateKbps = bitrateKbps,
                    Crf = 0,
                }
            );
        }

        return outputs;
    }

    /// <summary>
    /// Interpolate a bitrate for a non-table source resolution.
    ///
    /// Formula (linear in pixel-count space):
    ///   bitrate = lower.Bitrate + (upper.Bitrate - lower.Bitrate)
    ///             * (srcPixels - lower.Pixels) / (upper.Pixels - lower.Pixels)
    ///
    /// When no upper neighbour exists (source exceeds the largest table entry),
    /// the largest entry's bitrate is used directly.
    /// </summary>
    private static int InterpolateNativeBitrate(VideoCodecType codec, VideoStreamInfo source)
    {
        long srcPixels = (long)source.Width * source.Height;

        // Table is ordered descending by resolution — find the two neighbours.
        // "upper" = first entry with MORE pixels than source (i.e. above source).
        // "lower" = first entry with FEWER pixels (i.e. below or equal to source).
        TableRung? upper = DefaultTable.FirstOrDefault(t => (long)t.Width * t.Height > srcPixels);
        TableRung? lower = DefaultTable.FirstOrDefault(t => (long)t.Width * t.Height <= srcPixels);

        if (upper is null || lower is null)
        {
            // Source is at or above the largest entry — use the top rung's bitrate.
            return PickBitrate(codec, DefaultTable[0]);
        }

        long upperPixels = (long)upper.Width * upper.Height;
        long lowerPixels = (long)lower.Width * lower.Height;
        long range = upperPixels - lowerPixels;

        if (range <= 0)
            return PickBitrate(codec, lower);

        int upperBitrate = PickBitrate(codec, upper);
        int lowerBitrate = PickBitrate(codec, lower);
        double t = (double)(srcPixels - lowerPixels) / range;
        return (int)Math.Round(lowerBitrate + (upperBitrate - lowerBitrate) * t);
    }

    private static int PickBitrate(VideoCodecType codec, TableRung rung) =>
        codec switch
        {
            VideoCodecType.H265 => (int)Math.Round(rung.H264BitrateKbps * HevcMultiplier),
            VideoCodecType.Vp9 => (int)Math.Round(rung.H264BitrateKbps * Vp9Multiplier),
            VideoCodecType.Av1 => (int)Math.Round(rung.H264BitrateKbps * Av1Multiplier),
            _ => rung.H264BitrateKbps,
        };

    private sealed record TableRung(int Width, int Height, int H264BitrateKbps);
}
