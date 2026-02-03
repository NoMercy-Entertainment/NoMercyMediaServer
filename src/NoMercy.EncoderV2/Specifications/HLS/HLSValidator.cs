using NoMercy.EncoderV2.Validation;

namespace NoMercy.EncoderV2.Specifications.HLS;

/// <summary>
/// Validates HLS specifications and playlists
/// </summary>
public interface IHLSValidator
{
    Task<PlaylistValidationResult> ValidateSpecificationAsync(HLSSpecification spec);
    bool IsValidSegmentDuration(int duration);
    bool IsValidTargetDuration(int duration);
}

public class HLSValidator(IPlaylistValidator playlistValidator) : IHLSValidator
{
    public async Task<PlaylistValidationResult> ValidateSpecificationAsync(HLSSpecification spec)
    {
        PlaylistValidationResult result = new()
        {
            PlaylistType = "specification"
        };

        if (spec.Version < 1 || spec.Version > 7)
        {
            result.Errors.Add($"Invalid HLS version: {spec.Version}. Must be between 1 and 7.");
        }

        if (!IsValidTargetDuration(spec.TargetDuration))
        {
            result.Errors.Add($"Invalid target duration: {spec.TargetDuration}. Must be between 1 and 60 seconds.");
        }

        if (!IsValidSegmentDuration(spec.SegmentDuration))
        {
            result.Errors.Add($"Invalid segment duration: {spec.SegmentDuration}. Must be between 1 and {spec.TargetDuration} seconds.");
        }

        if (spec.SegmentDuration > spec.TargetDuration)
        {
            result.Warnings.Add($"Segment duration ({spec.SegmentDuration}s) should not exceed target duration ({spec.TargetDuration}s)");
        }

        if (spec.PlaylistType != "VOD" && spec.PlaylistType != "EVENT")
        {
            result.Warnings.Add($"Unusual playlist type: {spec.PlaylistType}. Typically 'VOD' or 'EVENT'.");
        }

        result.IsValid = result.Errors.Count == 0;

        return await Task.FromResult(result);
    }

    public bool IsValidSegmentDuration(int duration)
    {
        return duration >= 1 && duration <= 60;
    }

    public bool IsValidTargetDuration(int duration)
    {
        return duration >= 1 && duration <= 60;
    }
}
