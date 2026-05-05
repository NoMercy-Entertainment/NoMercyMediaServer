namespace NoMercy.Encoder.Profiles.V2;

public record HlsConfig(
    HlsPlaylistType PlaylistType = HlsPlaylistType.Vod,
    bool IndependentSegments = true,
    bool CmafCompatible = true,
    int? PartTargetDuration = null
);
