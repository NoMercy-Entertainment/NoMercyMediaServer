namespace NoMercy.Encoder.Strategies.Dash;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Storage;

/// <summary>
/// DASH 2-pass strategy — produces a segmented MPEG-DASH output with the
/// better bitrate distribution of 2-pass. Needed for Widevine / PlayReady
/// CENC integration in a future Phase 11 strategy, where the DRM processor
/// expects a DASH source.
/// </summary>
public class DashTwoPassStrategy(
    IEncoder encoder,
    ICheckpointStore checkpointStore,
    ILogger<DashTwoPassStrategy> logger,
    IStorage storage
) : TwoPassStrategyBase(encoder, checkpointStore, logger, storage)
{
    public override OutputFormat Format => OutputFormat.Dash;
}
