namespace NoMercy.Encoder.Subtitles;

/// <summary>
/// Thrown when the OpenSubtitles API responds with HTTP 429 or signals rate limiting.
/// The adapter catches this and returns an empty result without failing the encode.
/// </summary>
public class OpenSubtitlesRateLimitException : Exception
{
    public OpenSubtitlesRateLimitException()
        : base("OpenSubtitles rate limit exceeded") { }

    public OpenSubtitlesRateLimitException(string message)
        : base(message) { }
}
