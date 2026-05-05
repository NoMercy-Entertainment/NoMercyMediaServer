using NoMercy.Encoder.Codecs;

namespace NoMercy.Encoder.Profiles.V2;

public record ProfileValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings
);

public static class ProfileValidator
{
    public static ProfileValidationResult Validate(EncodingProfile profile)
    {
        List<string> errors = [];
        List<string> warnings = [];

        ValidateContainerCompatibility(profile, errors);

        return new(errors.Count == 0, errors, warnings);
    }

    private static void ValidateContainerCompatibility(EncodingProfile profile, List<string> errors)
    {
        if (profile.Video is { Policy: StreamPolicy.Transcode } video)
        {
            if (!ContainerCompatibility.SupportsVideo(profile.Container, video.Codec))
                errors.Add(
                    $"Container {profile.Container} does not support video codec {video.Codec}. {SuggestContainerForVideoCodec(video.Codec)}"
                );
        }

        foreach (AudioOutput audio in profile.Audio.Where(a => a.Policy == StreamPolicy.Transcode))
        {
            if (!ContainerCompatibility.SupportsAudio(profile.Container, audio.Codec))
                errors.Add(
                    $"Container {profile.Container} does not support audio codec {audio.Codec}. {SuggestContainerForAudioCodec(audio.Codec)}"
                );
        }
    }

    private static string SuggestContainerForVideoCodec(VideoCodecType codec)
    {
        IEnumerable<string> compatible = Enum.GetValues<Container>()
            .Where(c => ContainerCompatibility.SupportsVideo(c, codec))
            .Select(c => c.ToString());
        return $"Compatible containers for {codec}: {string.Join(", ", compatible)}.";
    }

    private static string SuggestContainerForAudioCodec(AudioCodecType codec)
    {
        IEnumerable<string> compatible = Enum.GetValues<Container>()
            .Where(c => ContainerCompatibility.SupportsAudio(c, codec))
            .Select(c => c.ToString());
        return $"Compatible containers for {codec}: {string.Join(", ", compatible)}.";
    }
}
