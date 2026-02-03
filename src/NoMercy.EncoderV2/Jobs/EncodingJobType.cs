namespace NoMercy.EncoderV2.Jobs;

/// <summary>
/// Job type for queue routing and processing strategy
/// </summary>
public enum EncodingJobType
{
    /// <summary>
    /// Video encoding job - routed to encoder:video queue (4 workers, parallel)
    /// </summary>
    Video = 0,
    
    /// <summary>
    /// Audio encoding job - routed to encoder:audio queue (2 workers, sequential)
    /// </summary>
    Audio = 1,
    
    /// <summary>
    /// Master HLS playlist generation - routed to encoder:master queue (1 worker, sequential)
    /// Only runs after video and audio jobs complete validation
    /// </summary>
    Master = 2
}
