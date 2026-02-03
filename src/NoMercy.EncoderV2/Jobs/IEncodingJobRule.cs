namespace NoMercy.EncoderV2.Jobs;

/// <summary>
/// Interface for encoding job rules/strategies
/// </summary>
public interface IEncodingJobRule
{
    string RuleName { get; }
    bool AppliesToJob(EncodingJobPayload job);
    Task<List<PostProcessingAction>> GetPostProcessingActionsAsync(EncodingJobPayload job);
}
