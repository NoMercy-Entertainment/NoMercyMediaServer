namespace NoMercy.Encoder.Strategies.Hls;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Storage;

/// <summary>
/// HLS 2-pass strategy. Pass 1 performs video-only analysis to a stats file,
/// pass 2 produces the final HLS output using those stats. See
/// <see cref="TwoPassStrategyBase"/> for the shared orchestration + checkpoint
/// resume logic.
/// </summary>
public class HlsTwoPassStrategy(
    IEncoder encoder,
    ICheckpointStore checkpointStore,
    ILogger<HlsTwoPassStrategy> logger,
    IStorage storage
) : TwoPassStrategyBase(encoder, checkpointStore, logger, storage)
{
    public override OutputFormat Format => OutputFormat.Hls;
}
