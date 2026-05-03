using NoMercy.Encoder.Profiles;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Metadata;

namespace NoMercy.OpticalMedia.Sources;

public enum RipMode
{
    RipAndEncode,
    RipToRaw,
}

public record RipRequest(
    string DrivePath,
    int[] SelectedTitleIndices,
    string? MetadataId,
    CustomMetadata? Custom,
    Ulid LibraryId,
    Ulid FolderId,
    string? EncodingProfileId,
    AudioTrackSelection[] AudioTracks,
    SubtitleSelection[] Subtitles,
    RipMode Mode = RipMode.RipAndEncode,
    /// <summary>
    /// Volume UUID of the disc's filesystem. When the OS can provide this it
    /// is used as the lock key so the same physical disc is recognised even if
    /// the device path changes. Falls back to <see cref="DrivePath"/> when
    /// <c>null</c> or empty.
    /// </summary>
    string? VolumeUuid = null,
    /// <summary>
    /// Resolved disc type. Controllers populate this from the drive monitor
    /// before dispatching the rip so DiscRipper can pick the right ffmpeg
    /// input shape (bluray:?playlist=N vs -f dvdvideo -title N vs cdda:).
    /// Defaults to <see cref="OpticalDiscType.None"/> for raw API calls
    /// where the caller didn't bother — DiscRipper falls back to detecting
    /// from the drive-path prefix in that case.
    /// </summary>
    OpticalDiscType DiscType = OpticalDiscType.None
);

public record CustomMetadata(string Title, int? Year, MediaType Type, string? PosterUrl);

public record AudioTrackSelection(int StreamIndex, bool Include);

public record SubtitleSelection(int StreamIndex, bool Include, SubtitleMode Mode);
