using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Encoder.BuildingBlocks;

/// <summary>
/// Produces a standard streaming ABR ladder from source media.
///
/// Legacy path: <see cref="Generate(MediaInfo, VideoOutput)"/> uses hardcoded
/// tier bitrates and replicates the pre-AutoLadderConfig behaviour exactly.
///
/// New path: <see cref="GenerateLadder(MediaInfo, VideoCodecType, AutoLadderConfig)"/>
/// reads every parameter from <see cref="AutoLadderConfig"/> — no hardcoded constants.
/// </summary>
public class AbrLadderGenerator : IAbrLadderGenerator
{
    private readonly ILogger<AbrLadderGenerator> _logger;

    public AbrLadderGenerator(ILogger<AbrLadderGenerator>? logger = null)
    {
        _logger = logger ?? NullLogger<AbrLadderGenerator>.Instance;
    }

    // ── Legacy constants (kept only for the legacy Generate path) ──────────

    private static readonly LegacyTierDef[] LegacyTiers =
    [
        new(Height: 360, DefaultBitrateKbps: 800),
        new(Height: 480, DefaultBitrateKbps: 1400),
        new(Height: 720, DefaultBitrateKbps: 3000),
        new(Height: 1080, DefaultBitrateKbps: 6000),
        new(Height: 1440, DefaultBitrateKbps: 9000),
        new(Height: 2160, DefaultBitrateKbps: 15000),
    ];

    // ── IAbrLadderGenerator — legacy path ──────────────────────────────────

    public VideoOutput[] Generate(MediaInfo media, VideoOutput reference)
    {
        if (media.VideoStreams.Count == 0)
            return [];

        VideoStreamInfo source = media.VideoStreams[0];
        double aspect = (double)source.Width / source.Height;
        double complexityScale = ComputeComplexityScale(source);

        List<VideoOutput> ladder = [];
        foreach (LegacyTierDef tier in LegacyTiers)
        {
            if (tier.Height > source.Height)
                continue;

            int width = EvenRound((int)(tier.Height * aspect));
            int height = tier.Height;
            int bitrate = Math.Max(200, (int)Math.Round(tier.DefaultBitrateKbps * complexityScale));

            ladder.Add(reference with { Width = width, Height = height, BitrateKbps = bitrate });
        }

        if (ladder.Count > 0 && ladder[^1].Height < source.Height)
        {
            int bitrate = Math.Max(
                LegacyTiers[^1].DefaultBitrateKbps,
                (int)Math.Round(source.BitRateKbps * complexityScale)
            );
            ladder.Add(
                reference with
                {
                    Width = source.Width,
                    Height = source.Height,
                    BitrateKbps = bitrate,
                }
            );
        }

        return ladder.ToArray();
    }

    // ── New path — reads AutoLadderConfig ──────────────────────────────────

    /// <summary>
    /// Generates an ABR ladder from <paramref name="autoConfig"/>.
    /// Every parameter (tiers, bitrate strategy, codec policy, framerate reduction,
    /// gap collapse, MaxRungs cap) comes from the config — no hardcoded constants.
    /// </summary>
    public LadderRung[] GenerateLadder(
        MediaInfo media,
        VideoCodecType profileCodec,
        AutoLadderConfig autoConfig
    )
    {
        if (autoConfig.Tiers.Length == 0)
            throw new InvalidOperationException(
                "AutoLadderConfig.Tiers is empty. ProfileValidator should have rejected this profile before reaching the generator."
            );

        if (media.VideoStreams.Count == 0)
            return [];

        VideoStreamInfo source = media.VideoStreams[0];

        // Step 1 — Filter candidate tiers.
        List<LadderTier> filtered = [];
        foreach (LadderTier tier in autoConfig.Tiers)
        {
            if (autoConfig.NeverUpscale && tier.Height > source.Height)
                continue;

            // NeverUpsource: compute the tier bitrate first to compare with source.
            // When the source bitrate is unknown (0 — ffprobe sometimes only fills
            // container-level bitrate, not the video stream's) the comparison would
            // cut every tier; skip the filter instead of producing zero rungs.
            if (autoConfig.NeverUpsource && source.BitRateKbps > 0)
            {
                int candidateBitrate = ComputeBitrate(tier, source, profileCodec, autoConfig);
                if (candidateBitrate > source.BitRateKbps)
                    continue;
            }

            filtered.Add(tier);
        }

        // Step 2 — Build rungs.
        List<LadderRung> rungs = [];
        foreach (LadderTier tier in filtered)
        {
            int bitrate = ComputeBitrate(tier, source, profileCodec, autoConfig);
            int maxBitrate = (int)Math.Round(bitrate * autoConfig.VbrCeilingMultiplier);
            int bufSize = (int)Math.Round(bitrate * autoConfig.BufferSizeMultiplier);

            VideoCodecType codec = SelectCodec(tier.Height, profileCodec, autoConfig);

            double framerate = source.FrameRate;
            if (
                autoConfig.ReduceFramerateForLowTiers
                && tier.Height <= autoConfig.LowTierFramerateThresholdHeight
            )
                framerate = source.FrameRate * autoConfig.LowTierFramerateMultiplier;

            rungs.Add(
                new LadderRung(
                    Width: tier.Width,
                    Height: tier.Height,
                    Codec: codec,
                    BitrateKbps: bitrate,
                    MaxBitrateKbps: maxBitrate,
                    BufferSizeKbps: bufSize,
                    Framerate: framerate
                )
            );
        }

        // Step 3 — Collapse rungs whose bitrates are within MinTierGapPercent of an adjacent rung.
        rungs = CollapseClose(rungs, autoConfig.MinTierGapPercent);

        // Step 4 — Cap to MaxRungs (retain highest-resolution rungs).
        if (rungs.Count > autoConfig.MaxRungs)
        {
            List<LadderRung> sorted = rungs.OrderByDescending(r => r.Height).ToList();
            rungs = sorted.Take(autoConfig.MaxRungs).ToList();
        }

        // Step 5 — Warn when below MinRungs; emit as-is (do not fabricate rungs).
        if (rungs.Count < autoConfig.MinRungs)
        {
            _logger.LogWarning(
                "AutoLadder produced {Actual} rung(s), which is below MinRungs={Min}. "
                    + "Emitting as-is — fabricating rungs to meet the floor is not supported.",
                rungs.Count,
                autoConfig.MinRungs
            );
        }

        return rungs.ToArray();
    }

    // ── Bitrate computation ────────────────────────────────────────────────

    private int ComputeBitrate(
        LadderTier tier,
        VideoStreamInfo source,
        VideoCodecType codec,
        AutoLadderConfig config
    )
    {
        switch (config.BitrateStrategy)
        {
            case BitrateStrategy.AppleHlsRecommended:
            case BitrateStrategy.BitrateLadder:
            {
                int? recommended = codec switch
                {
                    VideoCodecType.H265 => tier.RecommendedBitrateHevcKbps,
                    VideoCodecType.Av1 => tier.RecommendedBitrateAv1Kbps,
                    _ => tier.RecommendedBitrateH264Kbps,
                };

                if (recommended.HasValue)
                    return recommended.Value;

                // Null recommendation → fall through to CrfBased with a warning.
                _logger.LogWarning(
                    "Tier '{Label}' has no recommended bitrate for codec {Codec} "
                        + "and strategy {Strategy}. Falling back to CrfBased.",
                    tier.Label,
                    codec,
                    config.BitrateStrategy
                );

                return ComputeCrfBasedBitrate(tier.Height, config.Crf);
            }

            case BitrateStrategy.PercentOfSource:
            {
                if (source.BitRateKbps <= 0 || source.Height <= 0)
                    return ComputeCrfBasedBitrate(tier.Height, config.Crf);

                double ratio = (double)tier.Height / source.Height;
                return (int)
                    Math.Round(
                        source.BitRateKbps * ratio * ratio * config.SourcePercentage / 100.0
                    );
            }

            case BitrateStrategy.CrfBased:
                return ComputeCrfBasedBitrate(tier.Height, config.Crf);

            default:
                return ComputeCrfBasedBitrate(tier.Height, config.Crf);
        }
    }

    /// <summary>
    /// CRF-based bitrate ceiling: uses a reference bitrate per megapixel scaled by
    /// the CRF value (lower CRF = higher quality = higher bitrate ceiling).
    /// Reference: ~6000 kbps at 1080p CRF 22 → ~3.125 kbps/pixel.
    /// Formula: referenceBitrateKbpsPerPixel × pixels × qualityFactor
    /// where qualityFactor = (51 - crf) / (51 - 22).
    /// </summary>
    private static int ComputeCrfBasedBitrate(int heightPixels, int crf)
    {
        int safeHeight = Math.Max(heightPixels, 1);
        double pixels = 16.0 / 9.0 * safeHeight * safeHeight;
        double qualityFactor = Math.Clamp((51.0 - crf) / (51.0 - 22.0), 0.1, 3.0);
        const double referenceKbpsPerPixel = 0.003;
        return Math.Max(100, (int)Math.Round(pixels * referenceKbpsPerPixel * qualityFactor));
    }

    // ── Codec selection ────────────────────────────────────────────────────

    private static VideoCodecType SelectCodec(
        int tierHeight,
        VideoCodecType profileCodec,
        AutoLadderConfig config
    )
    {
        if (config.CodecPolicy == LadderCodecPolicy.Uniform)
            return profileCodec;

        // Mixed: requires both LowTierCodec and HighTierCodec — validator rejects null upstream.
        if (config.LowTierCodec is null || config.HighTierCodec is null)
            throw new InvalidOperationException(
                "AutoLadderConfig.CodecPolicy=Mixed requires both LowTierCodec and HighTierCodec. "
                    + "ProfileValidator should have rejected this profile before reaching the generator."
            );

        return tierHeight <= config.MixedPolicySplitHeight
            ? config.LowTierCodec.Value
            : config.HighTierCodec.Value;
    }

    // ── Gap collapse ───────────────────────────────────────────────────────

    /// <summary>
    /// Removes rungs whose bitrate is within <paramref name="minGapPercent"/> of the
    /// next higher rung. Iterates from lowest to highest; when two adjacent rungs are
    /// "too close", the lower one is dropped (keep the higher-quality rung).
    /// </summary>
    private static List<LadderRung> CollapseClose(List<LadderRung> rungs, double minGapPercent)
    {
        if (minGapPercent <= 0 || rungs.Count <= 1)
            return rungs;

        // Sort ascending by height so we compare low→high pairs.
        List<LadderRung> sorted = rungs.OrderBy(r => r.Height).ThenBy(r => r.BitrateKbps).ToList();
        List<LadderRung> result = [sorted[^1]];

        for (int i = sorted.Count - 2; i >= 0; i--)
        {
            LadderRung candidate = sorted[i];
            LadderRung keepRef = result[^1];

            double upperBitrate = (double)keepRef.BitrateKbps;
            double lowerBitrate = (double)candidate.BitrateKbps;

            if (upperBitrate <= 0)
            {
                result.Add(candidate);
                continue;
            }

            double gapPercent = Math.Abs(upperBitrate - lowerBitrate) / upperBitrate * 100.0;
            if (gapPercent >= minGapPercent)
                result.Add(candidate);
        }

        // result is built high→low; reverse to restore ascending order.
        result.Reverse();
        return result;
    }

    // ── Legacy helpers ─────────────────────────────────────────────────────

    private static double ComputeComplexityScale(VideoStreamInfo source)
    {
        if (source.BitRateKbps <= 0 || source.Width <= 0 || source.Height <= 0)
            return 1.0;

        double megapixels = (source.Width * source.Height) / 1_000_000.0;
        if (megapixels < 0.1)
            return 1.0;

        double kbpsPerMp = source.BitRateKbps / megapixels;
        double scale = kbpsPerMp / 1000.0;
        return Math.Clamp(scale, 0.5, 1.2);
    }

    private static int EvenRound(int value) => value - (value % 2);

    private record LegacyTierDef(int Height, int DefaultBitrateKbps);
}
