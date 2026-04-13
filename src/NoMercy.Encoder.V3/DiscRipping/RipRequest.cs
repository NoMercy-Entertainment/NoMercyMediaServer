namespace NoMercy.Encoder.V3.DiscRipping;

using NoMercy.Encoder.V3.Profiles;

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
    RipMode Mode = RipMode.RipAndEncode
);

public record CustomMetadata(string Title, int? Year, MediaType Type, string? PosterUrl);

public record AudioTrackSelection(int StreamIndex, bool Include);

public record SubtitleSelection(int StreamIndex, bool Include, SubtitleMode Mode);
