using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Storage;

namespace NoMercy.Encoder.Strategies.Mp4;

/// <summary>
/// MP4 2-pass strategy — pass 1 / pass 2 pattern produces a single faststart
/// .mp4 with the bits better distributed across complex scenes than
/// single-pass at the same target bitrate. Useful for downloadable
/// distribution where HLS segmentation is overkill.
/// </summary>
public class Mp4TwoPassStrategy(
    IEncoder encoder,
    ICheckpointStore checkpointStore,
    ILogger<Mp4TwoPassStrategy> logger,
    IStorage storage
) : TwoPassStrategyBase(encoder, checkpointStore, logger, storage)
{
    public override OutputFormat Format => OutputFormat.Mp4;
}
