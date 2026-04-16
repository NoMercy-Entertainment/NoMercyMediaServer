namespace NoMercy.Encoder.BuildingBlocks;

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Profiles;

/// <summary>
/// Produces a standard streaming ABR ladder below the source resolution using
/// the reference profile's codec + preset + encoder tuning.
///
/// Tier bitrates follow Apple HLS authoring guidelines (roughly): 360p 800 kbps,
/// 480p 1400 kbps, 720p 3000 kbps, 1080p 6000 kbps, 4K 15000 kbps. When the source
/// bitrate is known and low (anime, talking-head content), all tiers are scaled
/// down proportionally — no point encoding a 720p variant at 3000 kbps when the
/// source was 2500 kbps.
/// </summary>
public class AbrLadderGenerator : IAbrLadderGenerator
{
    private static readonly LadderTier[] Tiers =
    [
        new(Height: 360, DefaultBitrateKbps: 800),
        new(Height: 480, DefaultBitrateKbps: 1400),
        new(Height: 720, DefaultBitrateKbps: 3000),
        new(Height: 1080, DefaultBitrateKbps: 6000),
        new(Height: 1440, DefaultBitrateKbps: 9000),
        new(Height: 2160, DefaultBitrateKbps: 15000),
    ];

    public VideoOutput[] Generate(MediaInfo media, VideoOutput reference)
    {
        if (media.VideoStreams.Count == 0)
            return [];

        VideoStreamInfo source = media.VideoStreams[0];
        double aspect = (double)source.Width / source.Height;
        double complexityScale = ComputeComplexityScale(source);

        List<VideoOutput> ladder = [];
        foreach (LadderTier tier in Tiers)
        {
            // Skip tiers above the source — upscaling is never worth it.
            if (tier.Height > source.Height)
                continue;

            int width = EvenRound((int)(tier.Height * aspect));
            int height = tier.Height;
            int bitrate = Math.Max(200, (int)Math.Round(tier.DefaultBitrateKbps * complexityScale));

            ladder.Add(reference with { Width = width, Height = height, BitrateKbps = bitrate });
        }

        // Always include the source resolution itself when it doesn't match a tier
        // (e.g. 1200p source — the 1080p tier is the nearest but still downscales).
        // If the top ladder entry is smaller than source, add a native-resolution variant.
        if (ladder.Count > 0 && ladder[^1].Height < source.Height)
        {
            int bitrate = Math.Max(
                Tiers[^1].DefaultBitrateKbps,
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

    /// <summary>
    /// Rough complexity estimate from source bitrate density (kbps per megapixel).
    /// Dense bitrate = high complexity = keep default tier bitrates. Thin bitrate
    /// = simple content = scale tiers down. Returns 1.0 when unknown.
    /// </summary>
    private static double ComputeComplexityScale(VideoStreamInfo source)
    {
        if (source.BitRateKbps <= 0 || source.Width <= 0 || source.Height <= 0)
            return 1.0;

        double megapixels = (source.Width * source.Height) / 1_000_000.0;
        if (megapixels < 0.1)
            return 1.0;

        double kbpsPerMp = source.BitRateKbps / megapixels;

        // ~1000 kbps/Mp is typical well-encoded content. Below that, scale down.
        // Clamp to [0.5, 1.2] so we never collapse to nothing or blow up tiers.
        double scale = kbpsPerMp / 1000.0;
        return Math.Clamp(scale, 0.5, 1.2);
    }

    private static int EvenRound(int value) => value - (value % 2);

    private record LadderTier(int Height, int DefaultBitrateKbps);
}
