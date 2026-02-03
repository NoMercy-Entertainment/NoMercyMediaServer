namespace NoMercy.EncoderV2.Jobs;

/// <summary>
/// Represents a post-processing action after encoding completes
/// </summary>
public record PostProcessingAction
{
    public string ActionType { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? InputPath { get; init; }
    public string? OutputPath { get; init; }
    public string? Format { get; init; }
    public string? Language { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = [];
}
