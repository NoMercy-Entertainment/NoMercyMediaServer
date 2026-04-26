namespace NoMercy.Encoder.Codecs.Definitions;

/// <summary>
/// Synthetic codec definition for the <see cref="VideoCodecType.Copy"/>
/// passthrough. The single encoder entry uses ffmpeg's literal <c>copy</c>
/// codec name with no presets, profiles, levels, or rate-control modes —
/// every field is left empty so that anything reading the catalogue (the
/// dashboard form, the encoder picker, the benchmark candidate enumerator)
/// can immediately see "this codec has no knobs". RequiredVendor is null
/// because copy is software-only by definition; the encode pipeline never
/// materialises a hardware encoder for a copied stream.
/// </summary>
public class CopyVideoDefinition : ICodecDefinition
{
    public VideoCodecType CodecType => VideoCodecType.Copy;

    public EncoderInfo[] Encoders =>
        [
            new(
                FfmpegName: "copy",
                RequiredVendor: null,
                Presets: [],
                Profiles: [],
                Levels: [],
                // QualityRange is meaningless for stream copy — no quality
                // dial is ever applied. Use a sentinel range so any caller
                // accidentally reaching for QualityRange.Default sees zeros
                // and falls into a no-op rather than emitting CRF flags.
                QualityRange: new(Min: 0, Max: 0, Default: 0),
                SupportedRateControl: [],
                // Source bytes flow through unchanged, so 10-bit, HDR, and
                // every other source-side property are preserved verbatim.
                Supports10Bit: true,
                SupportsHdr: true,
                MaxConcurrentSessions: int.MaxValue,
                PixelFormat10Bit: string.Empty,
                VendorSpecificFlags: new()
            ),
        ];
}
