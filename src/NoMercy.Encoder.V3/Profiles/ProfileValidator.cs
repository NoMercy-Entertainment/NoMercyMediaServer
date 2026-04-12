namespace NoMercy.Encoder.V3.Profiles;

using NoMercy.Encoder.V3.Codecs;

public class ProfileValidator(CodecRegistry codecRegistry) : IProfileValidator
{
    public ValidationResult Validate(EncodingProfile profile)
    {
        List<ValidationError> errors = [];

        ValidateName(profile, errors);
        ValidateOutputsNotEmpty(profile, errors);
        ValidateVideoOutputs(profile, errors);
        ValidateAudioOutputs(profile, errors);
        ValidateFormatCompatibility(profile, errors);
        ValidateSubtitleCompatibility(profile, errors);

        bool isValid = errors.All(e => e.Severity != ValidationSeverity.Error);
        return new ValidationResult(isValid, [.. errors]);
    }

    private static void ValidateName(EncodingProfile profile, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            errors.Add(
                new ValidationError(
                    "Name",
                    "Profile must have a non-empty name.",
                    ValidationSeverity.Error
                )
            );
        }
    }

    private static void ValidateOutputsNotEmpty(
        EncodingProfile profile,
        List<ValidationError> errors
    )
    {
        if (profile.VideoOutputs.Length == 0 && profile.AudioOutputs.Length == 0)
        {
            errors.Add(
                new ValidationError(
                    "Outputs",
                    "Profile must have at least one video or audio output.",
                    ValidationSeverity.Error
                )
            );
        }
    }

    private void ValidateVideoOutputs(EncodingProfile profile, List<ValidationError> errors)
    {
        for (int i = 0; i < profile.VideoOutputs.Length; i++)
        {
            VideoOutput output = profile.VideoOutputs[i];
            string prefix = $"VideoOutput[{i}]";

            if (output.Width <= 0)
            {
                errors.Add(
                    new ValidationError(
                        $"{prefix}.Width",
                        "Width must be greater than 0.",
                        ValidationSeverity.Error
                    )
                );
            }

            if (output.BitrateKbps <= 0 && output.Crf <= 0)
            {
                errors.Add(
                    new ValidationError(
                        $"{prefix}.RateControl",
                        "Video output must specify either BitrateKbps > 0 or Crf > 0.",
                        ValidationSeverity.Error
                    )
                );
            }

            if (output.Crf > 0)
            {
                ICodecDefinition definition = codecRegistry.GetVideoDefinition(output.Codec);
                EncoderInfo? softwareEncoder = Array.Find(
                    definition.Encoders,
                    e => e.RequiredVendor == null
                );

                if (softwareEncoder != null)
                {
                    QualityRange range = softwareEncoder.QualityRange;
                    if (output.Crf < range.Min || output.Crf > range.Max)
                    {
                        errors.Add(
                            new ValidationError(
                                $"{prefix}.Crf",
                                $"Crf value {output.Crf} is outside the valid range [{range.Min}, {range.Max}] for {output.Codec}.",
                                ValidationSeverity.Error
                            )
                        );
                    }
                }
            }

            if (output.KeyframeIntervalSeconds < 0)
            {
                errors.Add(
                    new ValidationError(
                        $"{prefix}.KeyframeIntervalSeconds",
                        "KeyframeIntervalSeconds must be >= 0.",
                        ValidationSeverity.Error
                    )
                );
            }
        }
    }

    private void ValidateAudioOutputs(EncodingProfile profile, List<ValidationError> errors)
    {
        for (int i = 0; i < profile.AudioOutputs.Length; i++)
        {
            AudioOutput output = profile.AudioOutputs[i];
            string prefix = $"AudioOutput[{i}]";

            AudioEncoderInfo encoder = codecRegistry.GetAudioEncoder(output.Codec);

            if (!encoder.IsLossless && output.BitrateKbps <= 0)
            {
                errors.Add(
                    new ValidationError(
                        $"{prefix}.BitrateKbps",
                        $"BitrateKbps must be > 0 for lossy codec {output.Codec}.",
                        ValidationSeverity.Error
                    )
                );
            }

            if (!encoder.Channels.Contains(output.Channels))
            {
                errors.Add(
                    new ValidationError(
                        $"{prefix}.Channels",
                        $"{output.Channels} channels is not supported by {output.Codec}. Supported: [{string.Join(", ", encoder.Channels)}].",
                        ValidationSeverity.Error
                    )
                );
            }

            if (!encoder.SampleRates.Contains(output.SampleRateHz))
            {
                errors.Add(
                    new ValidationError(
                        $"{prefix}.SampleRateHz",
                        $"Sample rate {output.SampleRateHz} Hz is not supported by {output.Codec}. Supported: [{string.Join(", ", encoder.SampleRates)}].",
                        ValidationSeverity.Error
                    )
                );
            }

            if (!encoder.IsLossless && output.BitrateKbps > 0)
            {
                if (
                    output.BitrateKbps < encoder.MinBitrateKbps
                    || output.BitrateKbps > encoder.MaxBitrateKbps
                )
                {
                    errors.Add(
                        new ValidationError(
                            $"{prefix}.BitrateKbps",
                            $"BitrateKbps {output.BitrateKbps} is outside the valid range [{encoder.MinBitrateKbps}, {encoder.MaxBitrateKbps}] for {output.Codec}.",
                            ValidationSeverity.Error
                        )
                    );
                }
            }
        }
    }

    private static void ValidateFormatCompatibility(
        EncodingProfile profile,
        List<ValidationError> errors
    )
    {
        switch (profile.Format)
        {
            case OutputFormat.Hls:
                ValidateHlsCompatibility(profile, errors);
                break;

            case OutputFormat.Mp4:
                ValidateMp4Compatibility(profile, errors);
                break;

            case OutputFormat.Dash:
                ValidateDashCompatibility(profile, errors);
                break;

            case OutputFormat.Mkv:
                // MKV allows everything — no restrictions
                break;
        }
    }

    private static void ValidateHlsCompatibility(
        EncodingProfile profile,
        List<ValidationError> errors
    )
    {
        VideoCodecType[] allowedVideoCodecs =
        [
            VideoCodecType.H264,
            VideoCodecType.H265,
            VideoCodecType.Av1,
        ];
        AudioCodecType[] allowedAudioCodecs =
        [
            AudioCodecType.Aac,
            AudioCodecType.Ac3,
            AudioCodecType.Eac3,
            AudioCodecType.Opus,
        ];

        for (int i = 0; i < profile.VideoOutputs.Length; i++)
        {
            VideoOutput output = profile.VideoOutputs[i];
            if (!allowedVideoCodecs.Contains(output.Codec))
            {
                errors.Add(
                    new ValidationError(
                        $"VideoOutput[{i}].Codec",
                        $"{output.Codec} is not supported in HLS. Allowed: H264, H265, AV1.",
                        ValidationSeverity.Error
                    )
                );
            }
        }

        for (int i = 0; i < profile.AudioOutputs.Length; i++)
        {
            AudioOutput output = profile.AudioOutputs[i];
            if (!allowedAudioCodecs.Contains(output.Codec))
            {
                errors.Add(
                    new ValidationError(
                        $"AudioOutput[{i}].Codec",
                        $"{output.Codec} is not supported in HLS. Allowed: AAC, AC3, EAC3, Opus.",
                        ValidationSeverity.Error
                    )
                );
            }
        }
    }

    private static void ValidateMp4Compatibility(
        EncodingProfile profile,
        List<ValidationError> errors
    )
    {
        // H264/H265/AV1 are standard; VP9 in MP4 is non-standard → Warning
        for (int i = 0; i < profile.VideoOutputs.Length; i++)
        {
            VideoOutput output = profile.VideoOutputs[i];
            if (output.Codec == VideoCodecType.Vp9)
            {
                errors.Add(
                    new ValidationError(
                        $"VideoOutput[{i}].Codec",
                        "VP9 in MP4 container is non-standard and may have limited player support.",
                        ValidationSeverity.Warning
                    )
                );
            }
        }
    }

    private static void ValidateDashCompatibility(
        EncodingProfile profile,
        List<ValidationError> errors
    )
    {
        VideoCodecType[] allowedVideoCodecs =
        [
            VideoCodecType.H264,
            VideoCodecType.H265,
            VideoCodecType.Av1,
            VideoCodecType.Vp9,
        ];
        AudioCodecType[] allowedAudioCodecs =
        [
            AudioCodecType.Aac,
            AudioCodecType.Opus,
            AudioCodecType.Ac3,
            AudioCodecType.Eac3,
        ];

        for (int i = 0; i < profile.VideoOutputs.Length; i++)
        {
            VideoOutput output = profile.VideoOutputs[i];
            if (!allowedVideoCodecs.Contains(output.Codec))
            {
                errors.Add(
                    new ValidationError(
                        $"VideoOutput[{i}].Codec",
                        $"{output.Codec} is not supported in DASH. Allowed: H264, H265, AV1, VP9.",
                        ValidationSeverity.Error
                    )
                );
            }
        }

        for (int i = 0; i < profile.AudioOutputs.Length; i++)
        {
            AudioOutput output = profile.AudioOutputs[i];
            if (!allowedAudioCodecs.Contains(output.Codec))
            {
                errors.Add(
                    new ValidationError(
                        $"AudioOutput[{i}].Codec",
                        $"{output.Codec} is not supported in DASH. Allowed: AAC, Opus, AC3, EAC3.",
                        ValidationSeverity.Error
                    )
                );
            }
        }
    }

    private static void ValidateSubtitleCompatibility(
        EncodingProfile profile,
        List<ValidationError> errors
    )
    {
        for (int i = 0; i < profile.SubtitleOutputs.Length; i++)
        {
            SubtitleOutput output = profile.SubtitleOutputs[i];

            if (output.Mode != SubtitleMode.Extract)
            {
                // BurnIn and PassThrough are not format-restricted at this level
                continue;
            }

            switch (profile.Format)
            {
                case OutputFormat.Hls:
                    if (output.Codec == SubtitleCodecType.WebVtt)
                    {
                        // Valid — no issue
                    }
                    else if (output.Codec == SubtitleCodecType.Ass)
                    {
                        errors.Add(
                            new ValidationError(
                                $"SubtitleOutput[{i}].Codec",
                                "ASS subtitles in HLS Extract mode will be converted to WebVTT, losing styling.",
                                ValidationSeverity.Warning
                            )
                        );
                    }
                    else
                    {
                        errors.Add(
                            new ValidationError(
                                $"SubtitleOutput[{i}].Codec",
                                $"{output.Codec} is not supported in HLS Extract mode. Use WebVTT.",
                                ValidationSeverity.Error
                            )
                        );
                    }
                    break;

                case OutputFormat.Mp4:
                    if (
                        output.Codec != SubtitleCodecType.WebVtt
                        && output.Codec != SubtitleCodecType.Srt
                    )
                    {
                        errors.Add(
                            new ValidationError(
                                $"SubtitleOutput[{i}].Codec",
                                $"{output.Codec} is not supported in MP4 Extract mode. Use WebVTT or SRT.",
                                ValidationSeverity.Error
                            )
                        );
                    }
                    break;

                case OutputFormat.Mkv:
                case OutputFormat.Dash:
                    // All subtitle codecs allowed
                    break;
            }
        }
    }
}
