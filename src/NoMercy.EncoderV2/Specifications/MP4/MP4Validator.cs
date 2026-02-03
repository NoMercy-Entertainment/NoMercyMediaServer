namespace NoMercy.EncoderV2.Specifications.MP4;

/// <summary>
/// Validates MP4 files and specifications for encoder compatibility.
/// </summary>
public interface IMP4Validator
{
    Task<MP4ValidationResult> ValidateSpecificationAsync(MP4Specification spec);
    Task<MP4ValidationResult> ValidateTracksAsync(List<MP4Track> tracks);
    bool IsValidBrand(string brand);
    bool IsValidCodecFourCC(string fourCC, MP4TrackType trackType);
    bool IsValidLanguage(string language);
}

/// <summary>
/// MP4 validation result
/// </summary>
public class MP4ValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public string? ContainerType { get; set; } = "mp4";
}

/// <summary>
/// Validates MP4 files for encoder compatibility.
/// </summary>
public class MP4Validator : IMP4Validator
{
    // Valid brands
    private static readonly HashSet<string> ValidBrands =
    [
        // ISO base media
        "isom", "iso2", "iso3", "iso4", "iso5", "iso6", "iso7", "iso8", "iso9",
        // MP4
        "mp41", "mp42", "mp71",
        // Video codecs
        "avc1", "avc2", "avc3", "avc4", "avs2", "avs3",
        "hvc1", "hev1", "hvt1",
        "av01", "vp09",
        // Streaming
        "dash", "msdh", "msix", "cmfc", "cmff",
        // Apple
        "M4A ", "M4B ", "M4P ", "M4V ", "qt  ",
        // 3GPP
        "3gp4", "3gp5", "3gp6", "3gp7", "3gp8", "3gp9",
        "3g2a", "3g2b", "3g2c",
        // Other
        "f4v ", "f4p ", "f4a ", "f4b ", "NDAS", "NDSC", "NDSH", "NDSM", "NDSP", "NDSS", "NDXC", "NDXH", "NDXM", "NDXP", "NDXS"
    ];

    // Valid video codec FourCCs
    private static readonly HashSet<string> ValidVideoCodecs =
    [
        "avc1", "avc2", "avc3", "avc4", "avcp",  // H.264/AVC
        "hvc1", "hev1", "hvt1",                   // H.265/HEVC
        "av01",                                   // AV1
        "vp08", "vp09",                          // VP8/VP9
        "mp4v", "mp4s",                          // MPEG-4
        "s263", "h263",                          // H.263
        "dvhe", "dvh1",                          // Dolby Vision
        "encv",                                   // Encrypted video
        "resv"                                    // Restricted video
    ];

    // Valid audio codec FourCCs
    private static readonly HashSet<string> ValidAudioCodecs =
    [
        "mp4a",                                   // AAC, MP3, etc.
        "ac-3", "ec-3", "ac-4",                  // Dolby Audio
        "dtsc", "dtsh", "dtsl", "dtse", "dtsx",  // DTS
        "mlpa",                                   // TrueHD
        "Opus", "opus",                          // Opus
        "fLaC", "flac",                          // FLAC
        "alac",                                   // Apple Lossless
        "sowt", "twos", "raw ", "alaw", "ulaw",  // PCM variants
        "samr", "sawb", "sawp",                  // AMR
        "enca",                                   // Encrypted audio
        "spex"                                    // Speex
    ];

    // Valid subtitle/text codec FourCCs
    private static readonly HashSet<string> ValidTextCodecs =
    [
        "tx3g", "text",                          // 3GPP Timed Text
        "wvtt",                                   // WebVTT
        "stpp",                                   // TTML
        "c608", "c708",                          // CEA Closed Captions
        "subt",                                   // Subtitle track
        "sbtl",                                   // Subtitle
        "enct"                                    // Encrypted text
    ];

    // Valid ISO 639-2/T packed language codes
    private static readonly HashSet<string> CommonLanguageCodes =
    [
        "und", "eng", "spa", "fra", "deu", "ita", "por", "rus", "jpn", "kor",
        "zho", "chi", "ara", "hin", "ben", "pol", "ukr", "vie", "tha", "tur",
        "nld", "swe", "nor", "dan", "fin", "ces", "hun", "ron", "ell", "heb"
    ];

    public async Task<MP4ValidationResult> ValidateSpecificationAsync(MP4Specification spec)
    {
        MP4ValidationResult result = new();

        // Validate major brand
        if (string.IsNullOrEmpty(spec.MajorBrand))
        {
            result.Errors.Add("Major brand is required.");
        }
        else if (!IsValidBrand(spec.MajorBrand))
        {
            result.Warnings.Add($"Major brand '{spec.MajorBrand}' may not be widely supported.");
        }

        // Validate compatible brands
        if (spec.CompatibleBrands.Count == 0)
        {
            result.Warnings.Add("No compatible brands specified. Consider adding 'isom' and 'iso2'.");
        }
        else
        {
            foreach (string brand in spec.CompatibleBrands)
            {
                if (!IsValidBrand(brand))
                {
                    result.Warnings.Add($"Compatible brand '{brand}' may not be widely supported.");
                }
            }
        }

        // Validate movie timescale
        if (spec.MovieTimescale <= 0)
        {
            result.Errors.Add("Movie timescale must be positive.");
        }
        else if (spec.MovieTimescale < 600)
        {
            result.Warnings.Add($"Movie timescale {spec.MovieTimescale} is unusually low. Consider at least 600.");
        }

        // Validate fragment settings
        if (spec.Fragmented)
        {
            if (spec.FragmentDurationMs <= 0)
            {
                result.Errors.Add("Fragment duration must be positive for fragmented MP4.");
            }
            else if (spec.FragmentDurationMs < 500)
            {
                result.Warnings.Add("Fragment duration under 500ms may cause playback issues.");
            }
            else if (spec.FragmentDurationMs > 10000)
            {
                result.Warnings.Add("Fragment duration over 10 seconds may affect seek performance.");
            }
        }

        // CMAF requirements
        if (spec.CMAFCompliant)
        {
            if (!spec.Fragmented)
            {
                result.Errors.Add("CMAF compliance requires fragmented MP4.");
            }

            if (!spec.CompatibleBrands.Contains("cmfc") && !spec.CompatibleBrands.Contains("cmff"))
            {
                result.Warnings.Add("CMAF-compliant files should include 'cmfc' or 'cmff' in compatible brands.");
            }
        }

        // Validate language
        if (!IsValidLanguage(spec.DefaultLanguage))
        {
            result.Warnings.Add($"Default language '{spec.DefaultLanguage}' may not be a valid ISO 639-2 code.");
        }

        result.IsValid = result.Errors.Count == 0;
        return await Task.FromResult(result);
    }

    public async Task<MP4ValidationResult> ValidateTracksAsync(List<MP4Track> tracks)
    {
        MP4ValidationResult result = new();

        if (tracks.Count == 0)
        {
            result.Errors.Add("MP4 file must have at least one track.");
            result.IsValid = false;
            return await Task.FromResult(result);
        }

        HashSet<int> trackIds = [];
        bool hasVideoTrack = false;
        bool hasAudioTrack = false;

        foreach (MP4Track track in tracks)
        {
            // Validate track ID uniqueness
            if (track.TrackId <= 0)
            {
                result.Errors.Add($"Track ID must be positive: {track.TrackId}");
            }
            else if (!trackIds.Add(track.TrackId))
            {
                result.Errors.Add($"Duplicate track ID: {track.TrackId}");
            }

            // Validate codec FourCC
            if (string.IsNullOrEmpty(track.CodecFourCC))
            {
                result.Errors.Add($"Track {track.TrackId} has no codec FourCC.");
            }
            else if (!IsValidCodecFourCC(track.CodecFourCC, track.TrackType))
            {
                result.Warnings.Add($"Track {track.TrackId} has unusual codec FourCC: {track.CodecFourCC}");
            }

            // Validate handler type
            string expectedHandler = track.TrackType switch
            {
                MP4TrackType.Video => "vide",
                MP4TrackType.Audio => "soun",
                MP4TrackType.Text or MP4TrackType.Subtitle => "text",
                MP4TrackType.Hint => "hint",
                MP4TrackType.Meta => "meta",
                _ => ""
            };

            if (!string.IsNullOrEmpty(expectedHandler) &&
                !string.IsNullOrEmpty(track.HandlerType) &&
                track.HandlerType != expectedHandler)
            {
                result.Warnings.Add($"Track {track.TrackId} handler type '{track.HandlerType}' doesn't match track type. Expected '{expectedHandler}'.");
            }

            // Validate timescale
            if (track.Timescale <= 0)
            {
                result.Errors.Add($"Track {track.TrackId} timescale must be positive.");
            }

            // Validate video-specific
            if (track.TrackType == MP4TrackType.Video)
            {
                hasVideoTrack = true;

                if (track.Width <= 0 || track.Height <= 0)
                {
                    result.Errors.Add($"Video track {track.TrackId} must have positive width and height.");
                }

                if (track.Timescale < 1000)
                {
                    result.Warnings.Add($"Video track {track.TrackId} timescale {track.Timescale} is unusually low.");
                }
            }

            // Validate audio-specific
            if (track.TrackType == MP4TrackType.Audio)
            {
                hasAudioTrack = true;

                if (track.SampleRate <= 0)
                {
                    result.Errors.Add($"Audio track {track.TrackId} must have positive sample rate.");
                }

                if (track.ChannelCount <= 0)
                {
                    result.Errors.Add($"Audio track {track.TrackId} must have positive channel count.");
                }

                if (track.Timescale != track.SampleRate)
                {
                    result.Warnings.Add($"Audio track {track.TrackId} timescale ({track.Timescale}) differs from sample rate ({track.SampleRate}).");
                }
            }

            // Validate language
            if (!IsValidLanguage(track.Language))
            {
                result.Warnings.Add($"Track {track.TrackId} language '{track.Language}' may not be valid.");
            }
        }

        // Warn if no video or audio
        if (!hasVideoTrack && !hasAudioTrack)
        {
            result.Warnings.Add("MP4 file has no video or audio tracks.");
        }

        result.IsValid = result.Errors.Count == 0;
        return await Task.FromResult(result);
    }

    public bool IsValidBrand(string brand)
    {
        if (string.IsNullOrEmpty(brand))
        {
            return false;
        }

        // Brands should be exactly 4 characters
        if (brand.Length != 4)
        {
            return false;
        }

        return ValidBrands.Contains(brand) ||
               brand.All(c => char.IsLetterOrDigit(c) || c == ' '); // Accept any 4-char alphanumeric
    }

    public bool IsValidCodecFourCC(string fourCC, MP4TrackType trackType)
    {
        if (string.IsNullOrEmpty(fourCC))
        {
            return false;
        }

        return trackType switch
        {
            MP4TrackType.Video => ValidVideoCodecs.Contains(fourCC.ToLowerInvariant()) ||
                                  ValidVideoCodecs.Contains(fourCC),
            MP4TrackType.Audio => ValidAudioCodecs.Contains(fourCC.ToLowerInvariant()) ||
                                  ValidAudioCodecs.Contains(fourCC),
            MP4TrackType.Text or MP4TrackType.Subtitle => ValidTextCodecs.Contains(fourCC.ToLowerInvariant()) ||
                                                          ValidTextCodecs.Contains(fourCC),
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
            return CommonLanguageCodes.Contains(language.ToLowerInvariant()) ||
                   language.All(char.IsLetter);
        }

        // Check for ISO 639-1 (2-letter codes)
        if (language.Length == 2)
        {
            return language.All(char.IsLetter);
        }

        return false;
    }
}
