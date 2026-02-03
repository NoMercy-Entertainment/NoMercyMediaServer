using NoMercy.EncoderV2.Validation;

namespace NoMercy.EncoderV2.Specifications.MKV;

/// <summary>
/// Validates MKV files and specifications for encoder compatibility.
/// </summary>
public interface IMKVValidator
{
    Task<MKVValidationResult> ValidateSpecificationAsync(MKVSpecification spec);
    Task<MKVValidationResult> ValidateTracksAsync(List<MKVTrack> tracks);
    Task<MKVValidationResult> ValidateChaptersAsync(List<MKVChapter> chapters);
    bool IsValidCodecId(string codecId, MKVTrackType trackType);
    bool IsValidLanguage(string language);
}

/// <summary>
/// MKV validation result
/// </summary>
public class MKVValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public string? ContainerType { get; set; } = "mkv";
}

/// <summary>
/// Validates MKV files for encoder compatibility.
/// </summary>
public class MKVValidator : IMKVValidator
{
    // Valid video codec prefixes
    private static readonly HashSet<string> ValidVideoCodecPrefixes =
    [
        "V_MPEG4", "V_MPEGH", "V_VP8", "V_VP9", "V_AV1",
        "V_MPEG2", "V_MPEG1", "V_THEORA", "V_REAL", "V_MS",
        "V_UNCOMPRESSED", "V_QUICKTIME", "V_DIRAC", "V_PRORES"
    ];

    // Valid audio codec prefixes
    private static readonly HashSet<string> ValidAudioCodecPrefixes =
    [
        "A_AAC", "A_AC3", "A_EAC3", "A_DTS", "A_TRUEHD",
        "A_FLAC", "A_MPEG", "A_OPUS", "A_VORBIS", "A_PCM",
        "A_REAL", "A_QUICKTIME", "A_MPC", "A_WAVPACK", "A_TTA",
        "A_ALAC", "A_MS"
    ];

    // Valid subtitle codec prefixes
    private static readonly HashSet<string> ValidSubtitleCodecPrefixes =
    [
        "S_TEXT", "S_VOBSUB", "S_HDMV", "S_DVBSUB", "S_KATE", "S_USF"
    ];

    // ISO 639-2 language codes (partial list of common ones)
    private static readonly HashSet<string> ValidLanguageCodes =
    [
        "und", "eng", "spa", "fra", "deu", "ita", "por", "rus", "jpn", "kor",
        "zho", "chi", "ara", "hin", "ben", "pol", "ukr", "vie", "tha", "tur",
        "nld", "swe", "nor", "dan", "fin", "ces", "hun", "ron", "ell", "heb",
        "ind", "msa", "fil", "cat", "eus", "glg", "hrv", "srp", "slv", "bul"
    ];

    public async Task<MKVValidationResult> ValidateSpecificationAsync(MKVSpecification spec)
    {
        MKVValidationResult result = new();

        // Validate DocType version
        if (spec.DocTypeVersion < 1 || spec.DocTypeVersion > 4)
        {
            result.Errors.Add($"Invalid DocType version: {spec.DocTypeVersion}. Must be between 1 and 4.");
        }

        if (spec.DocTypeReadVersion < 1 || spec.DocTypeReadVersion > spec.DocTypeVersion)
        {
            result.Errors.Add($"DocTypeReadVersion ({spec.DocTypeReadVersion}) must be between 1 and DocTypeVersion ({spec.DocTypeVersion}).");
        }

        // Validate timestamp scale
        if (spec.TimestampScale <= 0)
        {
            result.Errors.Add("TimestampScale must be a positive value.");
        }
        else if (spec.TimestampScale > 1000000000)
        {
            result.Warnings.Add("TimestampScale exceeds 1 second precision which is unusual.");
        }

        // Validate cluster size
        if (spec.ClusterSizeBytes < 1024)
        {
            result.Warnings.Add($"Cluster size {spec.ClusterSizeBytes} bytes is very small. Consider at least 1KB.");
        }
        else if (spec.ClusterSizeBytes > 5242880)
        {
            result.Warnings.Add($"Cluster size {spec.ClusterSizeBytes} bytes exceeds 5MB which may affect seeking performance.");
        }

        // Validate language
        if (!IsValidLanguage(spec.DefaultLanguage))
        {
            result.Warnings.Add($"Default language '{spec.DefaultLanguage}' may not be a valid ISO 639-2 code.");
        }

        result.IsValid = result.Errors.Count == 0;
        return await Task.FromResult(result);
    }

    public async Task<MKVValidationResult> ValidateTracksAsync(List<MKVTrack> tracks)
    {
        MKVValidationResult result = new();

        if (tracks.Count == 0)
        {
            result.Errors.Add("MKV file must have at least one track.");
            result.IsValid = false;
            return await Task.FromResult(result);
        }

        HashSet<int> trackNumbers = [];
        HashSet<ulong> trackUids = [];

        bool hasVideoTrack = false;
        bool hasAudioTrack = false;

        foreach (MKVTrack track in tracks)
        {
            // Validate track number uniqueness
            if (track.TrackNumber <= 0)
            {
                result.Errors.Add($"Track number must be positive: {track.TrackNumber}");
            }
            else if (!trackNumbers.Add(track.TrackNumber))
            {
                result.Errors.Add($"Duplicate track number: {track.TrackNumber}");
            }

            // Validate track UID uniqueness
            if (track.TrackUid == 0)
            {
                result.Warnings.Add($"Track {track.TrackNumber} has no UID. One will be generated.");
            }
            else if (!trackUids.Add(track.TrackUid))
            {
                result.Errors.Add($"Duplicate track UID: {track.TrackUid}");
            }

            // Validate codec ID
            if (string.IsNullOrEmpty(track.CodecId))
            {
                result.Errors.Add($"Track {track.TrackNumber} has no codec ID.");
            }
            else if (!IsValidCodecId(track.CodecId, track.TrackType))
            {
                result.Warnings.Add($"Track {track.TrackNumber} has unusual codec ID: {track.CodecId}");
            }

            // Validate language
            if (!IsValidLanguage(track.Language))
            {
                result.Warnings.Add($"Track {track.TrackNumber} language '{track.Language}' may not be valid.");
            }

            // Track type presence
            if (track.TrackType == MKVTrackType.Video)
            {
                hasVideoTrack = true;
            }
            else if (track.TrackType == MKVTrackType.Audio)
            {
                hasAudioTrack = true;
            }
        }

        // Warn if no video or audio
        if (!hasVideoTrack && !hasAudioTrack)
        {
            result.Warnings.Add("MKV file has no video or audio tracks.");
        }

        result.IsValid = result.Errors.Count == 0;
        return await Task.FromResult(result);
    }

    public async Task<MKVValidationResult> ValidateChaptersAsync(List<MKVChapter> chapters)
    {
        MKVValidationResult result = new();

        if (chapters.Count == 0)
        {
            return await Task.FromResult(result);
        }

        HashSet<ulong> chapterUids = [];
        TimeSpan? previousStart = null;

        foreach (MKVChapter chapter in chapters)
        {
            // Validate chapter UID uniqueness
            if (chapter.ChapterUid == 0)
            {
                result.Warnings.Add($"Chapter '{chapter.Name}' has no UID. One will be generated.");
            }
            else if (!chapterUids.Add(chapter.ChapterUid))
            {
                result.Errors.Add($"Duplicate chapter UID: {chapter.ChapterUid}");
            }

            // Validate start time
            if (chapter.StartTime < TimeSpan.Zero)
            {
                result.Errors.Add($"Chapter '{chapter.Name}' has negative start time.");
            }

            // Validate end time
            if (chapter.EndTime.HasValue && chapter.EndTime.Value < chapter.StartTime)
            {
                result.Errors.Add($"Chapter '{chapter.Name}' end time is before start time.");
            }

            // Validate ordering
            if (previousStart.HasValue && chapter.StartTime < previousStart.Value)
            {
                result.Warnings.Add($"Chapter '{chapter.Name}' is not in chronological order.");
            }
            previousStart = chapter.StartTime;

            // Validate name
            if (string.IsNullOrWhiteSpace(chapter.Name))
            {
                result.Warnings.Add($"Chapter at {chapter.StartTime} has no name.");
            }

            // Validate language
            if (!IsValidLanguage(chapter.Language))
            {
                result.Warnings.Add($"Chapter '{chapter.Name}' language '{chapter.Language}' may not be valid.");
            }
        }

        result.IsValid = result.Errors.Count == 0;
        return await Task.FromResult(result);
    }

    public bool IsValidCodecId(string codecId, MKVTrackType trackType)
    {
        if (string.IsNullOrEmpty(codecId))
        {
            return false;
        }

        return trackType switch
        {
            MKVTrackType.Video => ValidVideoCodecPrefixes.Any(prefix => codecId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)),
            MKVTrackType.Audio => ValidAudioCodecPrefixes.Any(prefix => codecId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)),
            MKVTrackType.Subtitle => ValidSubtitleCodecPrefixes.Any(prefix => codecId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)),
            _ => true // Allow other track types without strict validation
        };
    }

    public bool IsValidLanguage(string language)
    {
        if (string.IsNullOrEmpty(language))
        {
            return false;
        }

        // Check for ISO 639-2 (3-letter codes)
        if (language.Length == 3)
        {
            return ValidLanguageCodes.Contains(language.ToLowerInvariant()) ||
                   language.All(char.IsLetter); // Accept any 3-letter code as potentially valid
        }

        // Check for ISO 639-1 (2-letter codes)
        if (language.Length == 2)
        {
            return language.All(char.IsLetter);
        }

        // Check for BCP-47 format (e.g., "en-US", "pt-BR")
        if (language.Contains('-'))
        {
            string[] parts = language.Split('-');
            return parts.Length >= 2 &&
                   parts[0].Length >= 2 && parts[0].Length <= 3 &&
                   parts[0].All(char.IsLetter);
        }

        return false;
    }
}
