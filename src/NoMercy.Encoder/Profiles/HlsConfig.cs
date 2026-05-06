namespace NoMercy.Encoder.Profiles;

public record HlsConfig(
    HlsPlaylistType PlaylistType = HlsPlaylistType.Vod,
    bool IndependentSegments = true,
    bool CmafCompatible = true,
    int? PartTargetDuration = null
);
