namespace NoMercy.Encoder.Devices;

public record VariantSelection(
    int? VariantIndex, // null = no existing variant fits; transcode required
    DeviceCapabilities? AppliedCapabilities, // null = no constraints applied (no caps declared)
    AudioConstraint? AudioConstraint, // non-null when transcode must downmix
    VideoConstraint? VideoConstraint, // non-null when transcode must downscale or transcode codec
    string? Reason // human-readable why-this-was-selected, for logs / dashboard
);

public record AudioConstraint(int Channels, string Codec);

public record VideoConstraint(int? MaxHeight, string? Codec);

public record VariantDescriptor(
    int Index,
    int Height,
    int Width,
    string VideoCodec,
    int VideoBitrateKbps,
    int AudioChannels,
    string AudioCodec
);
