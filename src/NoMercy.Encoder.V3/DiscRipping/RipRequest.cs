namespace NoMercy.Encoder.V3.DiscRipping;

using NoMercy.Encoder.V3.Profiles;

public record RipRequest(
    string DrivePath,
    int[] SelectedTitleIndices,
    string? MetadataId,
    CustomMetadata? Custom,
    string LibraryId,
    string FolderId,
    string? EncodingProfileId,
    AudioTrackSelection[] AudioTracks,
    SubtitleSelection[] Subtitles
);

public record CustomMetadata(string Title, int? Year, MediaType Type, string? PosterUrl);

public record AudioTrackSelection(int StreamIndex, bool Include);

public record SubtitleSelection(int StreamIndex, bool Include, SubtitleMode Mode);
