namespace NoMercy.Encoder.DiscRipping;

using NoMercy.Encoder.Profiles;

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
    string? VolumeUuid = null
);

public record CustomMetadata(string Title, int? Year, MediaType Type, string? PosterUrl);

public record AudioTrackSelection(int StreamIndex, bool Include);

public record SubtitleSelection(int StreamIndex, bool Include, SubtitleMode Mode);
