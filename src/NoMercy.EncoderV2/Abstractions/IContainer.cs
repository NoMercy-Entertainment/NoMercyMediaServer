namespace NoMercy.EncoderV2.Abstractions;

/// <summary>
/// Represents an output container format
/// </summary>
public interface IContainer
{
    /// <summary>
    /// The FFmpeg format name (e.g., "mp4", "matroska")
    /// </summary>
    string FormatName { get; }

    /// <summary>
    /// Human-readable display name
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// File extension (e.g., ".mp4", ".mkv")
    /// </summary>
    string Extension { get; }

    /// <summary>
    /// MIME type for this container
    /// </summary>
    string MimeType { get; }

    /// <summary>
    /// Whether this container supports streaming
    /// </summary>
    bool SupportsStreaming { get; }

    /// <summary>
    /// Compatible video codecs
    /// </summary>
    IReadOnlyList<string> CompatibleVideoCodecs { get; }

    /// <summary>
    /// Compatible audio codecs
    /// </summary>
    IReadOnlyList<string> CompatibleAudioCodecs { get; }

    /// <summary>
    /// Compatible subtitle codecs
    /// </summary>
    IReadOnlyList<string> CompatibleSubtitleCodecs { get; }

    /// <summary>
    /// Builds the FFmpeg arguments for this container
    /// </summary>
    IReadOnlyList<string> BuildArguments();

    /// <summary>
    /// Validates that the given codecs are compatible with this container
    /// </summary>
    ValidationResult ValidateCodecs(IVideoCodec? videoCodec, IAudioCodec? audioCodec, ISubtitleCodec? subtitleCodec);
}

/// <summary>
/// HLS-specific container for segmented streaming
/// </summary>
public interface IHlsContainer : IContainer
{
    /// <summary>
    /// Segment duration in seconds
    /// </summary>
    int SegmentDuration { get; set; }

    /// <summary>
    /// Playlist type (vod or event)
    /// </summary>
    HlsPlaylistType PlaylistType { get; set; }

    /// <summary>
    /// Segment filename pattern
    /// </summary>
    string SegmentFilenamePattern { get; set; }

    /// <summary>
    /// Master playlist filename
    /// </summary>
    string MasterPlaylistFilename { get; set; }

    /// <summary>
    /// Whether to include program date time
    /// </summary>
    bool IncludeProgramDateTime { get; set; }

    /// <summary>
    /// Whether to delete segments after concatenation
    /// </summary>
    bool DeleteSegments { get; set; }
}

public enum HlsPlaylistType
{
    Vod,
    Event
}

/// <summary>
/// Base container with common functionality
/// </summary>
public abstract class ContainerBase : IContainer
{
    public abstract string FormatName { get; }
    public abstract string DisplayName { get; }
    public abstract string Extension { get; }
    public abstract string MimeType { get; }
    public virtual bool SupportsStreaming => false;
    public abstract IReadOnlyList<string> CompatibleVideoCodecs { get; }
    public abstract IReadOnlyList<string> CompatibleAudioCodecs { get; }
    public abstract IReadOnlyList<string> CompatibleSubtitleCodecs { get; }

    public virtual IReadOnlyList<string> BuildArguments()
    {
        return ["-f", FormatName];
    }

    public virtual ValidationResult ValidateCodecs(IVideoCodec? videoCodec, IAudioCodec? audioCodec, ISubtitleCodec? subtitleCodec)
    {
        List<string> errors = [];

        if (videoCodec != null && !CompatibleVideoCodecs.Contains(videoCodec.Name))
        {
            errors.Add($"Video codec '{videoCodec.Name}' is not compatible with {DisplayName} container");
        }

        if (audioCodec != null && !CompatibleAudioCodecs.Contains(audioCodec.Name))
        {
            errors.Add($"Audio codec '{audioCodec.Name}' is not compatible with {DisplayName} container");
        }

        if (subtitleCodec != null && !CompatibleSubtitleCodecs.Contains(subtitleCodec.Name))
        {
            errors.Add($"Subtitle codec '{subtitleCodec.Name}' is not compatible with {DisplayName} container");
        }

        return errors.Count > 0 ? ValidationResult.Failure([.. errors]) : ValidationResult.Success();
    }
}
