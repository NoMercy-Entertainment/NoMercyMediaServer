using NoMercy.EncoderV2.Abstractions;

namespace NoMercy.EncoderV2.Profiles;

/// <summary>
/// Default implementation of IEncodingProfile
/// </summary>
public sealed class EncodingProfile : IEncodingProfile
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool IsSystem { get; init; }
    public required IContainer Container { get; init; }
    public IReadOnlyList<VideoOutputConfig> VideoOutputs { get; init; } = [];
    public IReadOnlyList<AudioOutputConfig> AudioOutputs { get; init; } = [];
    public IReadOnlyList<SubtitleOutputConfig> SubtitleOutputs { get; init; } = [];
    public ThumbnailConfig? ThumbnailConfig { get; init; }
    public EncodingOptions Options { get; init; } = new();

    public ValidationResult Validate()
    {
        List<string> errors = [];
        List<string> warnings = [];

        // Validate container
        if (Container == null)
        {
            errors.Add("Container is required");
        }

        // Validate video outputs
        foreach (VideoOutputConfig video in VideoOutputs)
        {
            if (video.Codec == null)
            {
                errors.Add($"Video output '{video.Id}' has no codec specified");
                continue;
            }

            // Validate codec settings
            ValidationResult codecValidation = video.Codec.Validate();
            if (!codecValidation.IsValid)
            {
                errors.AddRange(codecValidation.Errors.Select(e => $"Video '{video.Id}': {e}"));
            }
            warnings.AddRange(codecValidation.Warnings.Select(w => $"Video '{video.Id}': {w}"));

            // Validate container compatibility
            if (Container != null)
            {
                ValidationResult containerValidation = Container.ValidateCodecs(video.Codec, null, null);
                if (!containerValidation.IsValid)
                {
                    errors.AddRange(containerValidation.Errors);
                }
            }
        }

        // Validate audio outputs
        foreach (AudioOutputConfig audio in AudioOutputs)
        {
            if (audio.Codec == null)
            {
                errors.Add($"Audio output '{audio.Id}' has no codec specified");
                continue;
            }

            ValidationResult codecValidation = audio.Codec.Validate();
            if (!codecValidation.IsValid)
            {
                errors.AddRange(codecValidation.Errors.Select(e => $"Audio '{audio.Id}': {e}"));
            }
            warnings.AddRange(codecValidation.Warnings.Select(w => $"Audio '{audio.Id}': {w}"));

            if (Container != null)
            {
                ValidationResult containerValidation = Container.ValidateCodecs(null, audio.Codec, null);
                if (!containerValidation.IsValid)
                {
                    errors.AddRange(containerValidation.Errors);
                }
            }
        }

        // Validate subtitle outputs
        foreach (SubtitleOutputConfig subtitle in SubtitleOutputs)
        {
            if (subtitle.Codec == null)
            {
                errors.Add($"Subtitle output '{subtitle.Id}' has no codec specified");
                continue;
            }

            ValidationResult codecValidation = subtitle.Codec.Validate();
            if (!codecValidation.IsValid)
            {
                errors.AddRange(codecValidation.Errors.Select(e => $"Subtitle '{subtitle.Id}': {e}"));
            }
            warnings.AddRange(codecValidation.Warnings.Select(w => $"Subtitle '{subtitle.Id}': {w}"));

            if (Container != null)
            {
                ValidationResult containerValidation = Container.ValidateCodecs(null, null, subtitle.Codec);
                if (!containerValidation.IsValid)
                {
                    errors.AddRange(containerValidation.Errors);
                }
            }
        }

        // Validate thumbnail config
        if (ThumbnailConfig != null)
        {
            if (ThumbnailConfig.IntervalSeconds <= 0)
            {
                errors.Add("Thumbnail interval must be positive");
            }
            if (ThumbnailConfig.Width <= 0)
            {
                errors.Add("Thumbnail width must be positive");
            }
            if (ThumbnailConfig.Quality < 1 || ThumbnailConfig.Quality > 100)
            {
                errors.Add("Thumbnail quality must be between 1 and 100");
            }
        }

        if (errors.Count > 0)
        {
            return new ValidationResult { IsValid = false, Errors = errors, Warnings = warnings };
        }

        return warnings.Count > 0
            ? new ValidationResult { IsValid = true, Warnings = warnings }
            : ValidationResult.Success();
    }
}
