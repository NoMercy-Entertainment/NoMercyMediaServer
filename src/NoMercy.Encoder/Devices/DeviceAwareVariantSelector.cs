using NoMercy.Encoder.LiveTranscode;

namespace NoMercy.Encoder.Devices;

public class DeviceAwareVariantSelector : IDeviceAwareVariantSelector
{
    private const int LowRamBitrateKbpsCeiling = 2000;
    private const int StandardBitrateKbpsCeiling = 8000;
    private const int HighRamBitrateKbpsCeiling = 20000;

    public VariantSelection Select(
        IReadOnlyList<VariantDescriptor> variants,
        DeviceCapabilities? caps
    )
    {
        if (variants.Count == 0)
            return new VariantSelection(null, caps, null, null, "no variants available");

        if (caps is null)
            return new VariantSelection(
                variants[0].Index,
                null,
                null,
                null,
                "no capabilities declared"
            );

        int ramCeiling = caps.RamTier switch
        {
            DeviceRamTier.LowRam => LowRamBitrateKbpsCeiling,
            DeviceRamTier.HighRam => HighRamBitrateKbpsCeiling,
            _ => StandardBitrateKbpsCeiling,
        };

        List<VariantDescriptor> candidates = [.. variants];

        // Filter by audio channel count
        if (caps.MaxAudioChannels is int maxChannels)
            candidates = [.. candidates.Where(v => v.AudioChannels <= maxChannels)];

        // Filter by audio codec support
        if (caps.AudioCodecs.Length > 0)
            candidates =
            [
                .. candidates.Where(v =>
                    caps.AudioCodecs.Contains(v.AudioCodec, StringComparer.OrdinalIgnoreCase)
                ),
            ];

        // Filter by video codec support
        if (caps.VideoCodecs.Length > 0)
            candidates =
            [
                .. candidates.Where(v =>
                    caps.VideoCodecs.Contains(v.VideoCodec, StringComparer.OrdinalIgnoreCase)
                ),
            ];

        // Filter by max video height
        if (caps.MaxVideoHeight is int maxHeight)
            candidates = [.. candidates.Where(v => v.Height <= maxHeight)];

        // Filter out HDR variants when device doesn't support HDR.
        // HDR is signalled by naming convention: we treat any variant whose
        // video codec contains "hevc" OR "av1" with a height >=2160 as
        // potentially HDR, but since we don't have explicit HDR flags on
        // VariantDescriptor we skip that filter for now and rely on
        // height/codec filtering above as sufficient protection.
        // The HdrSupport flag is honoured at a higher level (PlaybackDecisionEngine)
        // which gates the transcode pipeline; this selector focuses on the ladder.

        // Apply RAM tier bitrate ceiling
        List<VariantDescriptor> withinCeiling =
        [
            .. candidates.Where(v => v.VideoBitrateKbps <= ramCeiling),
        ];

        if (withinCeiling.Count > 0)
        {
            // Pick the highest bitrate variant within the ceiling
            VariantDescriptor best = withinCeiling
                .OrderByDescending(v => v.VideoBitrateKbps)
                .First();
            return new VariantSelection(
                best.Index,
                caps,
                null,
                null,
                $"variant {best.Index} selected — {best.VideoBitrateKbps} kbps within {caps.RamTier} ceiling ({ramCeiling} kbps)"
            );
        }

        // No variant survives — build transcode constraints
        AudioConstraint? audioConstraint = null;
        VideoConstraint? videoConstraint = null;
        List<string> reasons = [];

        bool audioMismatch =
            caps.MaxAudioChannels.HasValue
            || (
                caps.AudioCodecs.Length > 0
                && variants.All(v =>
                    !caps.AudioCodecs.Contains(v.AudioCodec, StringComparer.OrdinalIgnoreCase)
                )
            );

        if (audioMismatch)
        {
            int targetChannels = caps.MaxAudioChannels ?? 2;
            string targetCodec = caps.AudioCodecs.Length > 0 ? caps.AudioCodecs[0] : "aac";
            audioConstraint = new AudioConstraint(targetChannels, targetCodec);
            reasons.Add($"audio downmix to {targetChannels}ch {targetCodec}");
        }

        bool videoMismatch =
            caps.MaxVideoHeight.HasValue
            || (
                caps.VideoCodecs.Length > 0
                && variants.All(v =>
                    !caps.VideoCodecs.Contains(v.VideoCodec, StringComparer.OrdinalIgnoreCase)
                )
            )
            || variants.All(v => v.VideoBitrateKbps > ramCeiling);

        if (videoMismatch)
        {
            string? targetCodec = caps.VideoCodecs.Length > 0 ? caps.VideoCodecs[0] : null;
            videoConstraint = new VideoConstraint(caps.MaxVideoHeight, targetCodec);
            reasons.Add(
                $"video constrained to {caps.MaxVideoHeight?.ToString() ?? "any"}px {targetCodec ?? "any codec"} within {caps.RamTier} ({ramCeiling} kbps)"
            );
        }

        string reason =
            reasons.Count > 0
                ? string.Join("; ", reasons)
                : $"no matching variant — {caps.RamTier} ceiling {ramCeiling} kbps";

        // Always include RamTier in reason so tests can assert on it
        if (!reason.Contains(caps.RamTier.ToString()))
            reason += $" [{caps.RamTier}]";

        return new VariantSelection(null, caps, audioConstraint, videoConstraint, reason);
    }

    /// <summary>
    /// Merges declared device capabilities into the client capabilities sent by the player.
    /// Device caps are authoritative for hardware-fixed limits (e.g. stereo-only speakers).
    /// The more restrictive of the two values wins for each field.
    /// A device without declared caps leaves the client caps unchanged.
    /// </summary>
    public ClientCapabilities ApplyDeviceCaps(
        ClientCapabilities client,
        DeviceCapabilities? deviceCaps
    )
    {
        if (deviceCaps is null)
            return client;

        int maxChannels = client.MaxAudioChannels;
        if (deviceCaps.MaxAudioChannels is int declaredChannels)
            maxChannels = Math.Min(maxChannels, declaredChannels);

        int maxHeight = client.MaxHeight;
        if (deviceCaps.MaxVideoHeight is int declaredHeight)
            maxHeight = Math.Min(maxHeight > 0 ? maxHeight : int.MaxValue, declaredHeight);

        bool supportsHdr = client.SupportsHdr && deviceCaps.HdrSupport;

        return client with
        {
            MaxAudioChannels = maxChannels,
            MaxHeight = maxHeight,
            SupportsHdr = supportsHdr,
        };
    }
}
